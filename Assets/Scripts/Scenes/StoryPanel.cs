using Cysharp.Text;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Scenes
{
    public class StoryPanel : MonoBehaviour
    {
        [SerializeField] private Image headerImage;
        [Tooltip("레벨 순서대로(레벨1=0번) 스토리 헤더에 표시할 이미지")]
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private GameObject[] levelPanels;
        [Tooltip("레벨 순서대로(레벨1=0번), 각 레벨 패널 안에서 LevelData.storyText를 표시할 텍스트(Text_LevelNStory)")]
        [SerializeField] private TMP_Text[] levelStoryTexts;
        [SerializeField] private Button closeButton;

        private ILogger<StoryPanel> _logger;

        /// <summary>
        /// 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<StoryPanel> logger)
        {
            _logger = logger;
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
        /// 현재 레벨의 헤더 이미지와 스토리 패널을 켜고 LevelData의 스토리 문구를 채운다.
        /// levelName은 현재 플레이 중인 레벨의 LevelData 에셋 이름(예: "02_WindData")으로,
        /// 진행도가 아닌 실제 로드된 레벨을 기준으로 골라야 testLevel 단독 테스트에서도 올바른 패널이 열린다.
        /// storyText가 null이면 패널에 이미 있는 텍스트를 그대로 둔다(LevelData 없이 호출하는 경우 대비).
        /// </summary>
        public void Show(string levelName, string storyText = null)
        {
            int levelNumber = Constants.Levels.ParseLevelNumber(levelName);
            string targetPanelName = levelNumber > 0 ? ZString.Concat("Level", levelNumber, "Panel") : null;

            // 헤더 이미지는 레벨 순서대로 배열에 들어 있다(레벨1=0번) — 스프라이트 파일명에 의존하지 않는다
            int headerIndex = levelNumber - 1;
            Sprite header = levelHeaderImages is not null && headerIndex >= 0 && headerIndex < levelHeaderImages.Length
                ? levelHeaderImages[headerIndex] : null;
            if (headerImage && header)
            {
                headerImage.sprite = header;
                headerImage.SetNativeSize();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[StoryPanel] 레벨{levelNumber}의 헤더 이미지가 할당되지 않았습니다.");
            }

            GameObject targetPanel = null;
            foreach (GameObject panel in levelPanels)
            {
                if (!panel) continue;
                bool match = panel.name == targetPanelName;
                panel.SetActive(match);
                if (match) targetPanel = panel;
            }

            // 2_Story와 같은 텍스트를 쓰도록 LevelData.storyText에서 가져와 Text_LevelNStory에 적용
            if (storyText is not null && targetPanel)
            {
                int index = levelNumber - 1;
                TMP_Text target = levelStoryTexts is not null && index >= 0 && index < levelStoryTexts.Length
                    ? levelStoryTexts[index] : null;
                if (target)
                    target.text = storyText;
                else if (_logger != null)
                    _logger.ZLogWarning($"[StoryPanel] 레벨{levelNumber}의 levelStoryTexts가 할당되지 않아 스토리 문구를 표시하지 않습니다.");
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 스토리 패널을 닫는다.
        /// </summary>
        private void Hide() => gameObject.SetActive(false);
    }
}
