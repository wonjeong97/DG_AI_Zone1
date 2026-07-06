using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            SetPanelInteractable(tutorialPanel, false);
            introPanel.alpha = 1f;
            SetPanelInteractable(introPanel, true);

            introStartButton.onClick.AddListener(OnIntroStartClicked);
            tutorialStartButton.onClick.AddListener(OnTutorialStartClicked);

            // WebGL은 StreamingAssets가 URL이라 VideoClip 소스를 지원하지 않음 — Url 소스로 미리 준비만 해두고
            // 실제 재생은 인트로 시작 버튼 클릭 시점에 시작
            if (videoPlayer)
            {
                videoPlayer.url = Path.Combine(Application.streamingAssetsPath, "Videos/Tutorial-webm.webm");
                videoPlayer.Prepare();
            }
        }

        private void OnDestroy()
        {
            introStartButton.onClick.RemoveListener(OnIntroStartClicked);
            tutorialStartButton.onClick.RemoveListener(OnTutorialStartClicked);
        }

        private void OnIntroStartClicked()
        {
            CrossFade(introPanel, tutorialPanel, crossFadeDuration, destroyCancellationToken).Forget();
            if (videoPlayer) videoPlayer.Play();
        }

        private void OnTutorialStartClicked()
        {
            SceneManager.LoadScene("2_Story");
        }

        // IntroPanel → TutorialPanel 크로스페이드
        private async UniTask CrossFade(CanvasGroup from, CanvasGroup to, float duration, CancellationToken ct)
        {
            SetPanelInteractable(to, true);

            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                if (from) from.alpha = 1f - p;
                if (to) to.alpha = p;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (from) from.alpha = 0f;
            if (to) to.alpha = 1f;
            SetPanelInteractable(from, false);
        }

        private static void SetPanelInteractable(CanvasGroup group, bool value)
        {
            if (!group) return;
            group.interactable = value;
            group.blocksRaycasts = value;
        }
    }
}
