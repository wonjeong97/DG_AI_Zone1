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

            // WebGL? StreamingAssets媛 URL?대씪 VideoClip ?뚯뒪瑜?吏?먰븯吏 ?딆쓬 ??Url ?뚯뒪濡?誘몃━ 以鍮꾨쭔 ?대몢怨?
            // ?ㅼ젣 ?ъ깮? ?명듃濡??쒖옉 踰꾪듉 ?대┃ ?쒖젏???쒖옉
            if (videoPlayer)
            {
                videoPlayer.url = Path.Combine(Application.streamingAssetsPath, "Videos/Tutorial_260710.webm");
                videoPlayer.Prepare();
                // destroyCancellationToken ?놁씠?????꾪솚?쇰줈 videoPlayer媛 ?뚭눼???ㅼ뿉???대쭅??怨꾩냽??MissingReferenceException 諛쒖깮
                SceneFader.RegisterPendingTask(UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: destroyCancellationToken));
            }

            // ?명듃濡??⑤꼸??濡쒕큸 ?곸긽 ??吏꾩엯怨??숈떆??猷⑦봽 ?ъ깮 (isLooping? 而댄룷?뚰듃???ㅼ젙??
            if (robotVideoPlayer)
            {
                robotVideoPlayer.url = Path.Combine(Application.streamingAssetsPath, "Videos/Robot_260710.webm");
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
