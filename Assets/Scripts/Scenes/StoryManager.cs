using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using UnityEngine;
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
                robotVideoPlayer.url = Path.Combine(Application.streamingAssetsPath, "Videos/Robot_260710.webm");
                robotVideoPlayer.Prepare();
                SceneFader.RegisterPendingTask(UniTask.WaitUntil(() => robotVideoPlayer.isPrepared, cancellationToken: destroyCancellationToken));
                robotVideoPlayer.Play();
            }
        }

        private void OnLevelButtonClicked(int index)
        {
            if (index < 0 || index >= levelDataList.Length) return;

            _currentLevel = levelDataList[index];
            if (headerImage && index < levelHeaderImages.Length)
                headerImage.sprite = levelHeaderImages[index];

            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (levelPanels[i]) levelPanels[i].SetActive(i == index);
            }

            TransitionToStoryPanelAsync().Forget();
        }

        // levelSelectPanel 페이드아웃 완료 후 storyPanel 페이드인
        private async UniTaskVoid TransitionToStoryPanelAsync()
        {
            try
            {
                CancellationToken ct = destroyCancellationToken;

                SceneFader.SetGroupInteractable(levelSelectPanel, false);
                await SceneFader.FadeCanvasGroupAsync(levelSelectPanel, 1f, 0f, fadeDuration, ct);

                SceneFader.SetGroupInteractable(storyPanel, true);
                await SceneFader.FadeCanvasGroupAsync(storyPanel, 0f, 1f, fadeDuration, ct);
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
            SceneFader.FadeAndLoad(_currentLevel.nextSceneName).Forget();
        }
    }
}
