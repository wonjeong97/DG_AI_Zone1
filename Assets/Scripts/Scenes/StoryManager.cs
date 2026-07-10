using System.IO;
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
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject storyPanel;
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private Image headerImage;
        [SerializeField] private TypewriterTextTMP storyText;
        [SerializeField] private Button startButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;

        [Inject] private GameSession _session;

        private LevelData _currentLevel;

        private void Start()
        {
            if (storyPanel) storyPanel.SetActive(false);
            if (levelSelectPanel) levelSelectPanel.SetActive(true);

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

            if (levelSelectPanel) levelSelectPanel.SetActive(false);
            // 패널을 먼저 켜야 TypewriterText.Awake가 실행됨 — 이후 SetText로 내용 지정
            if (storyPanel) storyPanel.SetActive(true);

            if (storyText && _currentLevel != null)
            {
                storyText.SetText(_currentLevel.storyText);
                storyText.PlayAsync(destroyCancellationToken).Forget();
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
