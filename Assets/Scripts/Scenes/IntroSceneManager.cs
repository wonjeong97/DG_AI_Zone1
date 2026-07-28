using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DG.Scenes
{
    public class IntroSceneManager : MonoBehaviour
    {
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private Button introStartButton;
        [SerializeField] private Button tutorialStartButton;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private float crossFadeDuration = 0.2f;

        private void Start()
        {
            tutorialPanel.alpha = 0f;
            SceneFader.SetGroupInteractable(tutorialPanel, false);
            introPanel.alpha = 1f;
            SceneFader.SetGroupInteractable(introPanel, true);

            introStartButton.onClick.AddListener(OnIntroStartClicked);
            tutorialStartButton.onClick.AddListener(OnTutorialStartClicked);
            
            if (videoPlayer)
            {
                videoPlayer.url = Constants.VideoPaths.TutorialUrl;
                videoPlayer.Prepare();
                SceneFader.RegisterPendingTask(UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: destroyCancellationToken));
            }
            
            if (robotVideoPlayer)
            {
                robotVideoPlayer.url = Constants.VideoPaths.RobotUrl;
                robotVideoPlayer.Prepare();
                SceneFader.RegisterPendingTask(UniTask.WaitUntil(() => robotVideoPlayer.isPrepared, cancellationToken: destroyCancellationToken));
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
            if (videoPlayer) videoPlayer.Play();
        }

        private void OnTutorialStartClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Story).Forget();
        }
    }
}
