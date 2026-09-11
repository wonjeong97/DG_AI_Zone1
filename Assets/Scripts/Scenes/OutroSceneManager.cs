using Cysharp.Threading.Tasks;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using Wonjeong.App;
using Wonjeong.Core;
using ZLogger;

namespace Scenes
{
    public class OutroSceneManager : MonoBehaviour
    {
        [SerializeField] private Button endButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private TMP_Text endingText;

        private ILogger<OutroSceneManager> _log;
        private InactivityTimer _inactivityTimer;
        private IPublisher<MoveIdleEvent> _moveIdlePublisher;

        [Inject]
        public void Construct(ILogger<OutroSceneManager> log, InactivityTimer inactivityTimer,
            IPublisher<MoveIdleEvent> moveIdlePublisher)
        {
            _log = log;
            _inactivityTimer = inactivityTimer;
            _moveIdlePublisher = moveIdlePublisher;
        }

        private void Start()
        {
            if (!endButton) _log?.ZLogWarning($"[OutroSceneManager] endButton이 할당되지 않았습니다.");
            if (!robotVideoPlayer) _log?.ZLogWarning($"[OutroSceneManager] robotVideoPlayer가 할당되지 않았습니다.");

            if (endButton)
                endButton.onClick.AddListener(OnEndButtonClicked);

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
            SceneFader.PlayLoopingVideo(robotVideoPlayer, Constants.VideoPaths.RobotUrl, destroyCancellationToken);

            PlayEndingTextAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid PlayEndingTextAsync(System.Threading.CancellationToken ct)
        {
            if (!endingText) return;

            // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
            endingText.ForceMeshUpdate();
            endingText.maxVisibleCharacters = 0;

            (float moveDuration, float interval, float yOffset) = await SceneFader.GetStoryLineSettingsAsync();
            await StoryLineAnimator.AnimateAsync(endingText,
                moveDuration, interval, yOffset,
                StoryLineAnimator.IsPointerPressedThisFrame, ct, _inactivityTimer);
        }

        private void OnDestroy()
        {
            if (endButton)
                endButton.onClick.RemoveListener(OnEndButtonClicked);
        }

        private void OnEndButtonClicked()
        {
            // 체험을 마치고 홈으로 돌아가는 정상 흐름 — 서버 통계에 move_idle로 집계되어야 함
            _moveIdlePublisher?.Publish(new MoveIdleEvent());
            SceneFader.FadeAndLoad(Constants.Scenes.Title).Forget();
        }
    }
}
