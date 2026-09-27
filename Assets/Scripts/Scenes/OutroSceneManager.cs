using Cysharp.Threading.Tasks;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using HuliacDev.App;
using HuliacDev.Core;
using ZLogger;

namespace Scenes
{
    public class OutroSceneManager : MonoBehaviour
    {
        [SerializeField] private Button endButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private TMP_Text endingText;

        private ILogger<OutroSceneManager> _logger;
        private InactivityTimer _inactivityTimer;
        private IPublisher<MoveIdleEvent> _moveIdlePublisher;

        /// <summary>
        /// 로거, 비활동 타이머, 관람 완료 이벤트 발행자를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<OutroSceneManager> log, InactivityTimer inactivityTimer,
            IPublisher<MoveIdleEvent> moveIdlePublisher)
        {
            _logger = log;
            _inactivityTimer = inactivityTimer;
            _moveIdlePublisher = moveIdlePublisher;
        }

        /// <summary>
        /// 종료 버튼·로봇 영상·엔딩 텍스트 연출을 시작한다.
        /// </summary>
        private void Start()
        {
            if (!endButton && _logger != null) _logger.ZLogWarning($"[OutroSceneManager] endButton이 할당되지 않았습니다.");
            if (!robotVideoPlayer && _logger != null) _logger.ZLogWarning($"[OutroSceneManager] robotVideoPlayer가 할당되지 않았습니다.");

            if (endButton)
                endButton.onClick.AddListener(OnEndButtonClicked);

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
            SceneFader.PlayLoopingVideo(robotVideoPlayer, Constants.VideoPaths.RobotUrl, destroyCancellationToken, _logger);

            PlayEndingTextAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 엔딩 텍스트를 한 줄씩 올라오며 페이드인되는 연출로 표시한다.
        /// </summary>
        private async UniTaskVoid PlayEndingTextAsync(System.Threading.CancellationToken ct)
        {
            if (!endingText)
            {
                if (_logger != null) _logger.ZLogWarning($"[OutroSceneManager] endingText가 할당되지 않아 엔딩 문구 연출을 건너뜁니다.");
                return;
            }

            // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
            endingText.ForceMeshUpdate();
            endingText.maxVisibleCharacters = 0;

            try
            {
                (float moveDuration, float interval, float yOffset) = await SceneFader.GetStoryLineSettingsAsync();
                await StoryLineAnimator.AnimateAsync(endingText,
                    moveDuration, interval, yOffset,
                    StoryLineAnimator.IsPointerPressedThisFrame, ct, _inactivityTimer);
            }
            catch (System.OperationCanceledException)
            {
                // 연출 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 종료 버튼 리스너를 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (endButton)
                endButton.onClick.RemoveListener(OnEndButtonClicked);
        }

        /// <summary>
        /// 체험을 마치고 홈으로 돌아간다 — 정상 관람 완료이므로 서버 통계에 move_idle로 집계한다.
        /// </summary>
        private void OnEndButtonClicked()
        {
            if (_moveIdlePublisher != null)
                _moveIdlePublisher.Publish(new MoveIdleEvent());
            else if (_logger != null)
                _logger.ZLogWarning($"[OutroSceneManager] MoveIdleEvent 발행자가 주입되지 않아 관람 완료 집계를 보내지 못했습니다.");
            SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
        }
    }
}
