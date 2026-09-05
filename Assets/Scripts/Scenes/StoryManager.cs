using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using Wonjeong.Core;
using Wonjeong.Utils;
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
        [SerializeField] private GameObject[] difficultyStars;
        [SerializeField] private Button startButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;

        private GameSession _session;
        private ILogger<StoryManager> _log;
        private InactivityTimer _inactivityTimer;

        // 00_Common.json의 panelFadeDuration 사용 — 로드 전까지의 폴백 기본값
        private float _fadeDuration = 0.3f;

        [Inject]
        public void Construct(GameSession session, ILogger<StoryManager> log, InactivityTimer inactivityTimer)
        {
            _session = session;
            _log = log;
            _inactivityTimer = inactivityTimer;
        }

        private LevelData _currentLevel;

        // 선택된 레벨 버튼이 storyPanel로 옮겨가 고정되는 위치 — 스토리 패널과 함께 보이도록 함
        private static readonly Vector2 SelectedLevelButtonPosition = new(-513f, -75f);

        private void Start()
        {
            if (!levelSelectPanel) _log?.ZLogWarning($"[StoryManager] levelSelectPanel이 할당되지 않았습니다.");
            if (!storyPanel) _log?.ZLogWarning($"[StoryManager] storyPanel이 할당되지 않았습니다.");
            if (!startButton) _log?.ZLogWarning($"[StoryManager] startButton이 할당되지 않았습니다.");

            // UI 작업 중 에디터에서 패널을 꺼둔 채 플레이해도 항상 levelSelectPanel만 보이는 상태로 시작하도록 정규화
            SceneFader.InitializePanelState(levelSelectPanel, true);
            SceneFader.InitializePanelState(storyPanel, false);

            // 레벨 상세 패널들도 씬 시작 시엔 전부 꺼진 상태여야 함 — 선택 시 OnLevelButtonClicked에서 다시 켜짐
            foreach (GameObject panel in levelPanels)
                if (panel) panel.SetActive(false);

            int unlockedIndex = Mathf.Clamp(_session ? _session.unlockedLevelIndex : 0, 0, levelDataList.Length - 1);

            // 난이도 표시 — 현재 플레이어가 선택할 수 있는(잠금 해제된) 레벨 수만큼 별을 활성화
            for (int i = 0; i < difficultyStars.Length; i++)
                if (difficultyStars[i]) difficultyStars[i].SetActive(i <= unlockedIndex);

            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (!levelButtons[i]) continue;

                int btnIndex = i;
                levelButtons[i].onClick.AddListener(() => OnLevelButtonClicked(btnIndex));

                if (i == unlockedIndex)
                {
                    levelButtons[i].interactable = true;
                    if (levelButtons[i].image) levelButtons[i].image.material = null;
                }
                else
                {
                    levelButtons[i].interactable = false;
                    if (levelButtons[i].image)
                        levelButtons[i].image.material = UiEffects.GrayscaleMaterial;
                }
            }

            startButton.onClick.AddListener(OnStartClicked);

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
            SceneFader.PlayLoopingVideo(robotVideoPlayer, Constants.VideoPaths.RobotUrl, destroyCancellationToken);
        }

        private void OnLevelButtonClicked(int index)
        {
            if (index < 0 || index >= levelDataList.Length) return;

            _currentLevel = levelDataList[index];

            // 선택한 레벨 버튼을 클릭 즉시 두 패널(levelSelectPanel·storyPanel) 바깥의 공통 부모로 옮김.
            // 두 패널 모두 CanvasGroup으로 페이드되는데, 그 자식으로 두면 페이드 도중 알파 블렌딩 때문에
            // 이미지가 흐릿하게 보여서, 페이드에 영향받지 않는 위치로 미리 빼둔다.
            // levelSelectPanel·storyPanel은 같은 부모 안에서 정확히 같은 영역을 꽉 채우고 있어 좌표계가 동일하므로
            // 이동해도 시각적으로 튀지 않는다. 실제 이동은 storyPanel이 페이드인되는 시점에 맞춰 트윈으로 처리한다
            RectTransform selectedButtonRect = null;
            if (levelButtons[index])
            {
                selectedButtonRect = (RectTransform)levelButtons[index].transform;
                selectedButtonRect.SetParent(storyPanel.transform.parent, worldPositionStays: false);
                levelButtons[index].interactable = false;

                // 버튼에 달려있던 별(Image_StarN) 아이콘은 스토리 패널로 넘어갈 땐 필요 없으므로 제거
                foreach (Transform child in selectedButtonRect)
                    child.gameObject.SetActive(false);
            }

            TMP_Text storyText = null;
            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (!levelPanels[i]) continue;
                levelPanels[i].SetActive(i == index);
                if (i == index) storyText = levelPanels[i].GetComponentInChildren<TMP_Text>(true);
            }

            // 2_Story·3_Game(스토리 다시보기)이 같은 텍스트를 쓰도록 LevelData.storyText에서 가져옴
            if (storyText != null)
                storyText.text = _currentLevel.storyText;

            // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
            if (storyText != null)
            {
                storyText.ForceMeshUpdate();
                storyText.maxVisibleCharacters = 0;
            }

            if (startButton) startButton.interactable = false;

            TransitionToStoryPanelAsync(storyText, selectedButtonRect).Forget();
        }

        // levelSelectPanel 페이드아웃 완료 후 storyPanel 페이드인, 스토리 텍스트가 한 줄씩 올라오는 연출이 끝나면 시작 버튼 활성화
        private async UniTaskVoid TransitionToStoryPanelAsync(TMP_Text storyText, RectTransform selectedButtonRect)
        {
            try
            {
                CancellationToken ct = destroyCancellationToken;
                _fadeDuration = await SceneFader.GetPanelFadeDurationAsync();

                SceneFader.SetGroupInteractable(levelSelectPanel, false);
                await SceneFader.FadeCanvasGroupAsync(levelSelectPanel, 1f, 0f, _fadeDuration, ct);

                SceneFader.SetGroupInteractable(storyPanel, true);

                // storyPanel이 페이드인되는 동안 선택된 레벨 버튼도 함께 제자리로 튀어 들어오도록(OutBack) 이동.
                // 이동 시간·반동 크기는 2_Story.json의 selectedLevelButtonMoveDuration/selectedLevelButtonMoveOvershoot로 재빌드 없이 조정 가능.
                // 페이드와 동시에 진행되어야 하므로 의도적으로 await하지 않는다
                if (selectedButtonRect)
                {
                    string settingsPath = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Story}";
                    StorySceneSettings sceneSettings = await JsonLoader.LoadAsync<StorySceneSettings>(settingsPath, ct);

                    _ = selectedButtonRect.DOAnchorPos(SelectedLevelButtonPosition, sceneSettings.selectedLevelButtonMoveDuration)
                        .SetEase(Ease.OutBack, sceneSettings.selectedLevelButtonMoveOvershoot)
                        .SetLink(selectedButtonRect.gameObject);
                }

                await SceneFader.FadeCanvasGroupAsync(storyPanel, 0f, 1f, _fadeDuration, ct);

                (float moveDuration, float interval, float yOffset) = await SceneFader.GetStoryLineSettingsAsync();
                await StoryLineAnimator.AnimateAsync(storyText,
                    moveDuration, interval, yOffset,
                    StoryLineAnimator.IsPointerPressedThisFrame, ct, _inactivityTimer);

                if (startButton) startButton.interactable = true;
            }
            catch (OperationCanceledException)
            {
                // 전환 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        private void OnStartClicked()
        {
            if (_currentLevel == null) return;
            if (_session) _session.currentLevel = _currentLevel;
            SceneFader.FadeAndLoad(_currentLevel.nextSceneName, logger: _log).Forget();
        }
    }
}
