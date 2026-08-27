using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace Scenes
{
    public class IntroSceneManager : MonoBehaviour
    {
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private Button introStartButton;
        [SerializeField] private Button tutorialStartButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private float crossFadeDuration = 0.2f;

        private ILogger<IntroSceneManager> _log;

        [Inject]
        public void Construct(ILogger<IntroSceneManager> log)
        {
            _log = log;
        }

        private void Start()
        {
            if (!introPanel) _log?.ZLogWarning($"[IntroSceneManager] introPanel이 할당되지 않았습니다.");
            if (!tutorialPanel) _log?.ZLogWarning($"[IntroSceneManager] tutorialPanel이 할당되지 않았습니다.");
            if (!introStartButton) _log?.ZLogWarning($"[IntroSceneManager] introStartButton이 할당되지 않았습니다.");
            if (!tutorialStartButton) _log?.ZLogWarning($"[IntroSceneManager] tutorialStartButton이 할당되지 않았습니다.");

            if (tutorialPanel)
            {
                tutorialPanel.alpha = 0f;
                SceneFader.SetGroupInteractable(tutorialPanel, false);
            }
            if (introPanel)
            {
                introPanel.alpha = 1f;
                SceneFader.SetGroupInteractable(introPanel, true);
            }

            if (introStartButton) introStartButton.onClick.AddListener(OnIntroStartClicked);
            if (tutorialStartButton) tutorialStartButton.onClick.AddListener(OnTutorialStartClicked);

            if (robotVideoPlayer)
            {
                SceneFader.ClearVideoRenderTexture(robotVideoPlayer);
                robotVideoPlayer.url = Constants.VideoPaths.RobotUrl;
                robotVideoPlayer.Prepare();
                SceneFader.RegisterPendingTask(SceneFader.WaitUntilVideoProgressAsync(robotVideoPlayer, Constants.VideoPaths.MinPlaybackProgressBeforeReveal, destroyCancellationToken));
                robotVideoPlayer.Play();
            }
        }

        private void OnDestroy()
        {
            introStartButton.onClick.RemoveListener(OnIntroStartClicked);
            tutorialStartButton.onClick.RemoveListener(OnTutorialStartClicked);
        }

        private void OnIntroStartClicked()
        {
            SceneFader.CrossFadeGroupsAsync(introPanel, tutorialPanel, crossFadeDuration, destroyCancellationToken).Forget();
        }

        private void OnTutorialStartClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Story).Forget();
        }
    }
}
