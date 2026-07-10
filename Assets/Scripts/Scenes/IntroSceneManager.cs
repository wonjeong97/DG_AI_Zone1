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
        [SerializeField] private float crossFadeDuration = 0.2f;

        private void Start()
        {
            tutorialPanel.alpha = 0f;
            SceneFader.SetGroupInteractable(tutorialPanel, false);
            introPanel.alpha = 1f;
            SceneFader.SetGroupInteractable(introPanel, true);

            introStartButton.onClick.AddListener(OnIntroStartClicked);
            tutorialStartButton.onClick.AddListener(OnTutorialStartClicked);

            // WebGL은 StreamingAssets가 URL이라 VideoClip 소스를 지원하지 않음 — Url 소스로 미리 준비만 해두고
            // 실제 재생은 인트로 시작 버튼 클릭 시점에 시작
            if (videoPlayer)
            {
                videoPlayer.url = Path.Combine(Application.streamingAssetsPath, "Videos/Tutorial-webm.webm");
                videoPlayer.Prepare();
                // destroyCancellationToken 없이는 씬 전환으로 videoPlayer가 파괴된 뒤에도 폴링이 계속돼 MissingReferenceException 발생
                SceneFader.RegisterPendingTask(UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: destroyCancellationToken));
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
            SceneFader.FadeAndLoad("2_Story").Forget();
        }
    }
}
