using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private TMP_Text endingText;

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

            if (endingText)
            {
                // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
                endingText.ForceMeshUpdate();
                endingText.maxVisibleCharacters = 0;
                StoryLineAnimator.AnimateAsync(endingText,
                    Constants.StoryLine.StoryLineMoveDuration,
                    Constants.StoryLine.StoryLineInterval,
                    Constants.StoryLine.StoryLineYOffset,
                    IsSkipRequested, destroyCancellationToken).Forget();
            }
        }

        // 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환함(연출 스킵용)
        private bool IsSkipRequested()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
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
