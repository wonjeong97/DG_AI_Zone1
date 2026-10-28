using System;
using System.Threading;
using App;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using HuliacDev.Utils;
using Network;
using ZLogger;

namespace Scenes
{
    public class StoryManager : MonoBehaviour
    {
        [SerializeField] private LevelData[] levelDataList;
        [SerializeField] private CanvasGroup levelSelectPanel;
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private GameObject[] levelPanels;
        [Tooltip("levelPanels와 같은 순서로, 각 레벨 패널 안에서 LevelData.storyText를 표시할 텍스트")]
        [SerializeField] private TMP_Text[] levelStoryTexts;
        [SerializeField] private Button startButton;
        [Tooltip("levelButtons와 같은 순서로, 결과 화면까지 간 레벨 버튼에 켤 V 표시")]
        [SerializeField] private GameObject[] levelCompleteMarks;
        [Tooltip("화면 좌상단 < 버튼 — 레벨 선택 화면이면 인트로로, 스토리 화면이면 레벨 선택 화면으로(관리자 레벨 이동이면 타이틀의 관리자 화면으로) 돌아간다")]
        [SerializeField] private Button backButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [Tooltip("선택한 레벨 버튼이 스토리 화면으로 옮겨가 고정되는 위치 — 스토리 패널과 함께 보이도록 둔다")]
        [SerializeField] private Vector2 selectedLevelButtonPosition = new(-513f, -75f);

        private GameSession _session;
        private ILogger<StoryManager> _logger;
        private InactivityTimer _inactivityTimer;
        private SoundManager _soundManager;
        private VisitorInfoProvider _visitorInfoProvider; // 행동 로그 주어
        private ArduinoUdpClient _arduino;

        /// <summary>
        /// 게임 세션, 로거, 비활동 타이머, 사운드 매니저, 체험자 정보, 아두이노 명령 클라이언트를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(GameSession session, ILogger<StoryManager> log, InactivityTimer inactivityTimer, SoundManager soundManager,
            VisitorInfoProvider visitorInfoProvider, ArduinoUdpClient arduino)
        {
            _session = session;
            _logger = log;
            _inactivityTimer = inactivityTimer;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
            _arduino = arduino;
        }

        private LevelData _currentLevel;

        /// <summary>
        /// 레벨 선택 화면을 초기화하고 결과 화면까지 간 레벨 버튼에 V를 켠다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[StoryManager] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            if (!levelSelectPanel && _logger != null) _logger.ZLogWarning($"[StoryManager] levelSelectPanel이 할당되지 않았습니다.");
            if (!storyPanel && _logger != null) _logger.ZLogWarning($"[StoryManager] storyPanel이 할당되지 않았습니다.");
            if (!startButton && _logger != null) _logger.ZLogWarning($"[StoryManager] startButton이 할당되지 않았습니다.");
            if (!backButton && _logger != null) _logger.ZLogWarning($"[StoryManager] backButton이 할당되지 않았습니다.");

            // UI 작업 중 에디터에서 패널을 꺼둔 채 플레이해도 항상 levelSelectPanel만 보이는 상태로 시작하도록 정규화
            SceneFader.InitializePanelState(levelSelectPanel, true);
            SceneFader.InitializePanelState(storyPanel, false);

            // 레벨 상세 패널들도 씬 시작 시엔 전부 꺼진 상태여야 함 — 선택 시 OnLevelButtonClicked에서 다시 켜짐
            foreach (GameObject panel in levelPanels)
                if (panel) panel.SetActive(false);

            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (!levelButtons[i]) continue;

                int btnIndex = i;
                levelButtons[i].onClick.AddListener(() => OnLevelButtonClicked(btnIndex));
            }

            ApplyCompleteMarks();

            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            if (backButton) backButton.onClick.AddListener(OnBackClicked);

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
            SceneFader.PlayRobotVideo(robotVideoPlayer, destroyCancellationToken, _logger);

            // 관리자 페이지에서 레벨을 골라 들어온 경우 — 레벨 선택 화면에서 그 레벨을 누른 것처럼 바로 스토리 화면으로 넘어간다
            if (_session != null && _session.pendingStoryLevelIndex >= 0)
            {
                int pendingIndex = _session.pendingStoryLevelIndex;
                _session.pendingStoryLevelIndex = -1;
                SelectLevel(pendingIndex);
            }
        }

        /// <summary>
        /// 모든 레벨 버튼은 처음부터 누를 수 있고, 결과 화면까지 간 레벨(서버에 기록이 있는 레벨 포함)의 버튼에만 V를 켠다.
        /// </summary>
        private void ApplyCompleteMarks()
        {
            if (levelCompleteMarks == null || levelCompleteMarks.Length < levelButtons.Length)
            {
                if (_logger != null) _logger.ZLogWarning($"[StoryManager] levelCompleteMarks가 레벨 버튼 수({levelButtons.Length}개)만큼 할당되지 않아 일부 레벨의 V 표시를 켜지 못합니다.");
                if (levelCompleteMarks == null) return;
            }

            for (int i = 0; i < levelCompleteMarks.Length; i++)
            {
                if (!levelCompleteMarks[i]) continue;
                levelCompleteMarks[i].SetActive(_session != null && _session.completedLevelIndices.Contains(i));
            }
        }

        /// <summary>
        /// 버튼 리스너를 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
            if (backButton) backButton.onClick.RemoveListener(OnBackClicked);
        }

        /// <summary>
        /// 레벨 버튼 클릭 효과음을 내고 그 레벨을 고른다.
        /// </summary>
        private void OnLevelButtonClicked(int index)
        {
            if (_logger != null) _logger.ZLogInformation($"[StoryManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} {index + 1}레벨을 고름.");
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            SelectLevel(index);
        }

        /// <summary>
        /// 선택한 레벨의 스토리 패널을 준비하고 레벨 선택 화면에서 스토리 화면으로 전환한다.
        /// </summary>
        private void SelectLevel(int index)
        {
            if (index < 0 || index >= levelDataList.Length)
            {
                if (_logger != null) _logger.ZLogWarning($"[StoryManager] {index}번 레벨이 levelDataList에 없어 선택하지 않습니다.");
                return;
            }

            _currentLevel = levelDataList[index];

            // 스토리 화면이 나오는 순간 아두이노 조명·장치를 이 레벨로 바꾼다 — 관리자 페이지에서 레벨을 골라 들어온 경우도 같다
            if (_arduino != null) _arduino.SendEnter(_currentLevel.levelIndex);
            else if (_logger != null) _logger.ZLogWarning($"[StoryManager] ArduinoUdpClient가 주입되지 않아 아두이노에 {index + 1}레벨 enter를 보내지 못했습니다.");

            // 선택한 레벨 버튼을 클릭 즉시 두 패널(levelSelectPanel·storyPanel) 바깥의 공통 부모로 옮김.
            // 두 패널 모두 CanvasGroup으로 페이드되는데, 그 자식으로 두면 페이드 도중 알파 블렌딩 때문에
            // 이미지가 흐릿하게 보여서, 페이드에 영향받지 않는 위치로 미리 빼둔다.
            // levelSelectPanel·storyPanel은 같은 부모 안에서 정확히 같은 영역을 꽉 채우고 있어 좌표계가 동일하므로
            // 이동해도 시각적으로 튀지 않는다. 실제 이동은 storyPanel이 페이드인되는 시점에 맞춰 트윈으로 처리한다
            RectTransform selectedButtonRect = null;
            Button selectedButton = index < levelButtons.Length ? levelButtons[index] : null;
            if (selectedButton && storyPanel)
            {
                selectedButtonRect = (RectTransform)selectedButton.transform;
                selectedButtonRect.SetParent(storyPanel.transform.parent, worldPositionStays: false);
                selectedButton.interactable = false;

                // 버튼에 달려있던 별(Image_StarN) 아이콘은 스토리 패널로 넘어갈 땐 필요 없으므로 제거
                foreach (Transform child in selectedButtonRect)
                    child.gameObject.SetActive(false);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[StoryManager] {index}번 레벨 버튼이나 storyPanel이 없어 고른 레벨 버튼을 스토리 화면으로 옮기지 않습니다.");
            }

            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (!levelPanels[i]) continue;
                levelPanels[i].SetActive(i == index);
            }

            TMP_Text storyText = levelStoryTexts != null && index < levelStoryTexts.Length ? levelStoryTexts[index] : null;
            if (storyText)
            {
                // 2_Story·3_Game(스토리 다시보기)이 같은 텍스트를 쓰도록 LevelData.storyText에서 가져옴
                storyText.text = _currentLevel.storyText;

                // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
                StoryLineAnimator.HideBeforeAnimate(storyText);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[StoryManager] {index}번 레벨의 levelStoryTexts가 할당되지 않아 스토리 문구를 표시하지 않습니다.");
            }

            if (startButton) startButton.interactable = false;

            TransitionToStoryPanelAsync(storyText, selectedButtonRect).Forget();
        }

        /// <summary>
        /// levelSelectPanel 페이드아웃 완료 후 storyPanel을 페이드인하고, 스토리 텍스트 연출이 끝나면 시작 버튼을 활성화한다.
        /// </summary>
        private async UniTaskVoid TransitionToStoryPanelAsync(TMP_Text storyText, RectTransform selectedButtonRect)
        {
            try
            {
                CancellationToken ct = destroyCancellationToken;
                float fadeDuration = await SceneFader.GetPanelFadeDurationAsync();

                SceneFader.SetGroupInteractable(levelSelectPanel, false);
                await SceneFader.FadeCanvasGroupAsync(levelSelectPanel, 1f, 0f, fadeDuration, ct);

                SceneFader.SetGroupInteractable(storyPanel, true);

                // storyPanel이 페이드인되는 동안 선택된 레벨 버튼도 함께 제자리로 튀어 들어오도록(OutBack) 이동.
                // 이동 시간·반동 크기는 2_Story.json의 selectedLevelButtonMoveDuration/selectedLevelButtonMoveOvershoot로 재빌드 없이 조정 가능.
                // 페이드와 동시에 진행되어야 하므로 의도적으로 await하지 않는다
                if (selectedButtonRect)
                {
                    StorySceneSettings sceneSettings = await JsonLoader.LoadAsync<StorySceneSettings>(Constants.SettingsFiles.Story, ct, _logger);

                    // JsonLoader는 파일을 읽다가 오류가 나면 취소를 다시 확인하지 않고 기본값을 돌려주므로, 파괴된 버튼에 트윈을 걸지 않도록 여기서 취소를 전달한다
                    ct.ThrowIfCancellationRequested();

                    _ = selectedButtonRect.DOAnchorPos(selectedLevelButtonPosition, sceneSettings.selectedLevelButtonMoveDuration)
                        .SetEase(Ease.OutBack, sceneSettings.selectedLevelButtonMoveOvershoot)
                        .SetLink(selectedButtonRect.gameObject);
                }

                await SceneFader.FadeCanvasGroupAsync(storyPanel, 0f, 1f, fadeDuration, ct);

                if (storyText)
                {
                    await StoryLineAnimator.AnimateWithCommonSettingsAsync(storyText, ct, _inactivityTimer, _logger);
                }

                if (startButton) startButton.interactable = true;
            }
            catch (OperationCanceledException)
            {
                // 전환 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// &lt; 버튼 — 레벨 선택 화면이면 인트로로, 스토리 화면이면 관리자 레벨 이동으로 들어온 판은 타이틀의 관리자 화면으로, 아니면 레벨 선택 화면으로 돌아간다.
        /// </summary>
        private void OnBackClicked()
        {
            // 레벨 선택 화면은 이 씬을 다시 불러 되돌린다(고른 레벨 버튼을 옮기고 별을 숨긴 연출을 하나씩 되돌리지 않기 위해).
            // 씬 전환 중에는 받지 않는다 — 홈 버튼과 함께 눌려 다른 씬으로 가는 중이면 여기서 보낼 로그·명령이 없다
            if (SceneFader.IsLoading) return;
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);

            // 레벨을 고르기 전이면 아두이노에 enter를 보내지 않았으므로 exit 없이 인트로로 간다
            if (!_currentLevel)
            {
                if (_logger != null) _logger.ZLogInformation($"[StoryManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 레벨 선택 화면에서 뒤로를 누름 — 인트로로 돌아감.");
                SceneFader.FadeAndLoad(Constants.Scenes.Intro, logger: _logger).Forget();
                return;
            }

            bool toAdmin = _session != null && _session.isAdminLevelJump;
            if (_logger != null) _logger.ZLogInformation($"[StoryManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 뒤로를 누름 — {(toAdmin ? "관리자 화면" : "레벨 선택")}으로 돌아감.");

            // 고른 레벨을 그만두므로 아두이노 조명·장치를 그 레벨 상태로 두지 않는다
            if (_arduino != null) _arduino.SendExit();
            else if (_logger != null) _logger.ZLogWarning($"[StoryManager] ArduinoUdpClient가 주입되지 않아 뒤로를 눌렀을 때 아두이노에 exit를 보내지 못했습니다.");

            if (toAdmin)
            {
                _session.openAdminOnTitle = true;
                SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
                return;
            }

            SceneFader.FadeAndLoad(Constants.Scenes.Story, logger: _logger).Forget();
        }

        /// <summary>
        /// 시작 버튼 클릭 시 선택한 레벨을 세션에 기록하고 게임 씬으로 넘어간다.
        /// </summary>
        private void OnStartClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            if (!_currentLevel)
            {
                if (_logger != null) _logger.ZLogWarning($"[StoryManager] 선택된 레벨이 없어 게임 씬으로 넘어가지 않습니다.");
                return;
            }
            if (_logger != null) _logger.ZLogInformation($"[StoryManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} {_currentLevel.levelIndex + 1}레벨 스토리를 보고 시작을 누름.");
            if (_session != null) _session.currentLevel = _currentLevel;
            SceneFader.FadeAndLoad(Constants.Scenes.Game, logger: _logger).Forget();
        }
    }
}
