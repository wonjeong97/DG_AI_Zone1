using Cysharp.Text;
using Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace Scenes
{
    public class HintPanel : MonoBehaviour
    {
        [SerializeField] private GameObject[] levelPanels;
        [SerializeField] private Button closeButton;

        [Tooltip("레벨1 힌트의 시간대별(8AM/10AM/12PM/2PM/4PM) 오브젝트를 담은 부모 — 문제 시간에 맞는 것만 표시")]
        [SerializeField] private Transform level1TimeContainer;

        [Tooltip("레벨2 힌트의 풍향별(Image_Wind_East/West/South/North) 오브젝트를 담은 부모(Level2Panel) — 문제 풍향에 맞는 것만 표시")]
        [SerializeField] private Transform level2WindContainer;

        [Tooltip("레벨3(수력) 힌트의 강물 높이 텍스트(Text_Meter) — 문제 높이 값을 그대로 표시")]
        [SerializeField] private TMP_Text level3MeterText;

        [Tooltip("레벨3(수력) 힌트의 수문 규칙 문구 텍스트 — 문제 높이 값을 넣어 '높으면 열기 / 낮으면 닫기'를 표시")]
        [SerializeField] private TMP_Text level3RuleText;

        [Tooltip("레벨4(발전소) 힌트 오브젝트를 담은 부모(Level4Panel) — 문제 변형별 토글 로직 추가 시 사용")]
        [SerializeField] private Transform level4PowerPlantContainer;

        [Tooltip("레벨5(미래 에너지) 힌트 오브젝트를 담은 부모(Level5Panel) — 문제 변형별 토글 로직 추가 시 사용")]
        [SerializeField] private Transform level5FutureEnergyContainer;

        private ILogger<HintPanel> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거와 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<HintPanel> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 닫기 버튼에 숨기기 동작을 연결한다.
        /// </summary>
        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// 현재 레벨의 힌트 패널만 켜고 문제 값에 맞는 변형을 표시한다.
        /// level은 현재 플레이 중인 레벨로, 진행도가 아닌 실제 로드된 레벨을 기준으로 골라야 testLevel 단독 테스트에서도 올바른 패널이 열린다.
        /// </summary>
        public void Show(LevelData level, string questionValueKey = null)
        {
            int levelNumber = Constants.Levels.ParseLevelNumber(level ? level.name : null);
            string targetPanelName = levelNumber > 0 ? ZString.Concat("Level", levelNumber, "Panel") : null;

            foreach (GameObject panel in levelPanels)
            {
                if (panel) panel.SetActive(panel.name == targetPanelName);
            }

            if (level1TimeContainer)
                level1TimeContainer.gameObject.SetActive(level1TimeContainer.gameObject.name == targetPanelName);

            if (levelNumber == 1)
                ApplyHintVariant(level1TimeContainer, nameof(level1TimeContainer), level, questionValueKey);

            if (levelNumber == 2)
                ApplyHintVariant(level2WindContainer, nameof(level2WindContainer), level, questionValueKey);

            if (levelNumber == 3)
                ApplyMeterText(level, questionValueKey);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 힌트 그림 오브젝트 중 현재 문제 값에 해당하는 것만 켠다 — 그림 이름은 LevelData의 문제 값 후보에 적혀 있다.
        /// </summary>
        private void ApplyHintVariant(Transform container, string containerName, LevelData level, string questionValueKey)
        {
            if (!container)
            {
                if (_logger != null) _logger.ZLogWarning($"[HintPanel] {containerName}가 할당되지 않아 힌트 그림을 고르지 못했습니다.");
                return;
            }

            if (!level)
            {
                if (_logger != null) _logger.ZLogWarning($"[HintPanel] 레벨 정보가 없어 {containerName}의 힌트 그림을 고르지 못했습니다.");
                return;
            }

            QuestionOption current = level.FindQuestionOption(questionValueKey);
            string target = current is not null ? current.hintObjectName : null;
            bool isTargetFound = false;
            foreach (Transform child in container)
            {
                // 힌트 그림 오브젝트만 토글 — Image_Horizon·Image_Windforce 등 공통 배경은 그대로 둠
                if (!IsHintObjectName(level, child.name)) continue;

                bool isTarget = child.name == target;
                child.gameObject.SetActive(isTarget);
                isTargetFound |= isTarget;
            }

            if (!isTargetFound && _logger != null)
                _logger.ZLogWarning($"[HintPanel] 문제 값 '{questionValueKey}'의 힌트 그림({target})을 {containerName}에서 찾지 못했습니다.");
        }

        /// <summary>
        /// 자식 오브젝트 이름이 이 레벨의 문제 값 후보에 적힌 힌트 그림 이름인지 확인한다.
        /// </summary>
        private static bool IsHintObjectName(LevelData level, string objectName)
        {
            if (level.questionOptions is null) return false;

            foreach (QuestionOption option in level.questionOptions)
                if (option is not null && !string.IsNullOrEmpty(option.hintObjectName) && option.hintObjectName == objectName)
                    return true;
            return false;
        }

        /// <summary>
        /// 레벨3 힌트 패널의 Text_Meter에 현재 문제의 강물 높이 값을 그대로 표시하고, 같은 높이로 수문 규칙 문구를 채운다.
        /// </summary>
        private void ApplyMeterText(LevelData level, string questionValueKey)
        {
            if (level3MeterText)
                level3MeterText.text = questionValueKey;
            else if (_logger != null)
                _logger.ZLogWarning($"[HintPanel] level3MeterText가 할당되지 않아 강물 높이를 표시하지 못했습니다.");

            if (!level3RuleText)
            {
                if (_logger != null) _logger.ZLogWarning($"[HintPanel] level3RuleText가 할당되지 않아 수문 규칙 문구를 표시하지 못했습니다.");
            }
            else if (level && !string.IsNullOrEmpty(level.hintRuleFormat))
            {
                level3RuleText.text = ZString.Format(level.hintRuleFormat, questionValueKey);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[HintPanel] 레벨에 힌트 규칙 문구(hintRuleFormat)가 없어 수문 규칙 문구를 표시하지 못했습니다.");
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭음을 내고 힌트 패널을 닫는다.
        /// </summary>
        private void Hide()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            gameObject.SetActive(false);
        }
    }
}
