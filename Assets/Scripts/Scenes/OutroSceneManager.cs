using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DG.Scenes
{
    public class OutroSceneManager : MonoBehaviour
    {
        [SerializeField] private Button endButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;

        private void Start()
        {
            if (endButton)
                endButton.onClick.AddListener(OnEndButtonClicked);

            // 濡쒕큸 ?곸긽 ??吏꾩엯怨??숈떆??猷⑦봽 ?ъ깮 (isLooping? 而댄룷?뚰듃???ㅼ젙??
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
            if (endButton)
                endButton.onClick.RemoveListener(OnEndButtonClicked);
        }

        private void OnEndButtonClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Title).Forget();
        }
    }
}
