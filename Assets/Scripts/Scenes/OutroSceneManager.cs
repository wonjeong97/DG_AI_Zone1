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
    public class OutroSceneManager : MonoBehaviour
    {
        [SerializeField] private Button endButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;

        private ILogger<OutroSceneManager> _log;

        [Inject]
        public void Construct(ILogger<OutroSceneManager> log)
        {
            _log = log;
        }

        private void Start()
        {
            if (!endButton) _log?.ZLogWarning($"[OutroSceneManager] endButton이 할당되지 않았습니다.");
            if (!robotVideoPlayer) _log?.ZLogWarning($"[OutroSceneManager] robotVideoPlayer가 할당되지 않았습니다.");

            if (endButton)
                endButton.onClick.AddListener(OnEndButtonClicked);

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
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
            if (endButton)
                endButton.onClick.RemoveListener(OnEndButtonClicked);
        }

        private void OnEndButtonClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Title).Forget();
        }
    }
}
