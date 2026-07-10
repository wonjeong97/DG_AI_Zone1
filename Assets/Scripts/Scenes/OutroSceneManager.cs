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

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
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
            SceneFader.FadeAndLoad("0_Title").Forget();
        }
    }
}
