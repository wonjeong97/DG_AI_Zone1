using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using VContainer;

namespace DG.Scenes
{
    public class StoryManager : MonoBehaviour
    {
        [SerializeField] private LevelData[] levelDataList;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private CanvasGroup levelSelectPanel;
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private Image headerImage;
        [SerializeField] private GameObject[] levelPanels;
        [SerializeField] private Button startButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private float fadeDuration = 0.3f;

        [Inject] private GameSession _session;

        private LevelData _currentLevel;

        private void Start()
        {
            if (storyPanel)
            {
                storyPanel.alpha = 0f;
                SceneFader.SetGroupInteractable(storyPanel, false);
            }

            if (levelSelectPanel)
            {
                levelSelectPanel.alpha = 1f;
                SceneFader.SetGroupInteractable(levelSelectPanel, true);
            }

            int unlockedIndex = Mathf.Clamp(_session ? _session.unlockedLevelIndex : 0, 0, levelDataList.Length - 1);

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

            // 롸벗 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
            if (robotVideoPlayer)
            {
                SceneFader.ClearVideoRenderTexture(robotVideoPlayer);
                robotVideoPlayer.url = Constants.VideoPaths.RobotUrl;
                robotVideoPlayer.Prepare();
                SceneFader.RegisterPendingTask(SceneFader.WaitUntilVideoProgressAsync(robotVideoPlayer, Constants.VideoPaths.MinPlaybackProgressBeforeReveal, destroyCancellationToken));
                robotVideoPlayer.Play();
            }
        }

        private void OnLevelButtonClicked(int index)
        {
            if (index < 0 || index >= levelDataList.Length) return;

            _currentLevel = levelDataList[index];
            if (headerImage && index < levelHeaderImages.Length)
                headerImage.sprite = levelHeaderImages[index];

            TMP_Text storyText = null;
            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (!levelPanels[i]) continue;
                levelPanels[i].SetActive(i == index);
                if (i == index) storyText = levelPanels[i].GetComponentInChildren<TMP_Text>(true);
            }

            // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
            if (storyText != null)
            {
                storyText.ForceMeshUpdate();
                storyText.maxVisibleCharacters = 0;
            }

            if (startButton) startButton.interactable = false;

            TransitionToStoryPanelAsync(storyText).Forget();
        }

        // levelSelectPanel 페이드아웃 완료 후 storyPanel 페이드인, 스토리 텍스트가 한 줄씩 올라오는 연출이 끝나면 시작 버튼 활성화
        private async UniTaskVoid TransitionToStoryPanelAsync(TMP_Text storyText)
        {
            try
            {
                CancellationToken ct = destroyCancellationToken;

                SceneFader.SetGroupInteractable(levelSelectPanel, false);
                await SceneFader.FadeCanvasGroupAsync(levelSelectPanel, 1f, 0f, fadeDuration, ct);

                SceneFader.SetGroupInteractable(storyPanel, true);
                await SceneFader.FadeCanvasGroupAsync(storyPanel, 0f, 1f, fadeDuration, ct);

                await StoryLineAnimator.AnimateAsync(storyText,
                    Constants.StoryLine.StoryLineMoveDuration,
                    Constants.StoryLine.StoryLineInterval,
                    Constants.StoryLine.StoryLineYOffset,
                    IsSkipRequested, ct);

                if (startButton) startButton.interactable = true;
            }
            catch (OperationCanceledException)
            {
                // 전환 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        // 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환함(연출 스킵용)
        private bool IsSkipRequested()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        private void OnStartClicked()
        {
            if (_currentLevel == null) return;
            if (_session) _session.currentLevel = _currentLevel;
            SceneFader.FadeAndLoad(_currentLevel.nextSceneName).Forget();
        }
    }
}
