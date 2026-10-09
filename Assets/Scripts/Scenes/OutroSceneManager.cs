using App;
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
using HuliacDev.UI;
using ZLogger;

namespace Scenes
{
    public class OutroSceneManager : MonoBehaviour
    {
        [SerializeField] private Button endButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private TMP_Text endingText;

        private ILogger<OutroSceneManager> _logger;
        private VisitorInfoProvider _visitorInfoProvider;
        private InactivityTimer _inactivityTimer;
        private IPublisher<MoveIdleEvent> _moveIdlePublisher;
        private SoundManager _soundManager;

        // 종료 버튼을 이미 눌렀는지 — 연타해도 관람 완료 집계(move_idle)가 한 번만 나가도록
        private bool _isLeaving;

        /// <summary>
        /// 로거, 체험자 정보 제공자, 비활동 타이머, 관람 완료 이벤트 발행자, 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<OutroSceneManager> log, VisitorInfoProvider visitorInfoProvider,
            InactivityTimer inactivityTimer, IPublisher<MoveIdleEvent> moveIdlePublisher, SoundManager soundManager)
        {
            _logger = log;
            _visitorInfoProvider = visitorInfoProvider;
            _inactivityTimer = inactivityTimer;
            _moveIdlePublisher = moveIdlePublisher;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 종료 버튼·로봇 영상·엔딩 텍스트 연출을 시작한다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[OutroSceneManager] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            if (!endButton && _logger != null) _logger.ZLogWarning($"[OutroSceneManager] endButton이 할당되지 않았습니다.");
            if (!robotVideoPlayer && _logger != null) _logger.ZLogWarning($"[OutroSceneManager] robotVideoPlayer가 할당되지 않았습니다.");

            if (endButton)
                endButton.onClick.AddListener(OnEndButtonClicked);

            // 로봇 영상 — 진입과 동시에 루프 재생 (isLooping은 컴포넌트에 설정됨)
            SceneFader.PlayLoopingVideo(robotVideoPlayer, Constants.VideoPaths.RobotUrl, destroyCancellationToken, _logger);

            PlayEndingTextAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// "{name}" 플레이스홀더를 체험자 이름으로 치환한 뒤 엔딩 텍스트를 한 줄씩 올라오며 페이드인되는 연출로 표시한다.
        /// </summary>
        private async UniTaskVoid PlayEndingTextAsync(System.Threading.CancellationToken ct)
        {
            if (!endingText)
            {
                if (_logger != null) _logger.ZLogWarning($"[OutroSceneManager] endingText가 할당되지 않아 엔딩 문구 연출을 건너뜁니다.");
                return;
            }

            StoryLineAnimator.HideBeforeAnimate(endingText);

            if (_visitorInfoProvider != null)
                endingText.text = _visitorInfoProvider.FillName(endingText.text);
            else if (_logger != null)
                _logger.ZLogWarning($"[OutroSceneManager] VisitorInfoProvider가 주입되지 않아 이름을 치환하지 않습니다.");

            try
            {
                await StoryLineAnimator.AnimateWithCommonSettingsAsync(endingText, ct, _inactivityTimer, _logger);
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
            if (_isLeaving) return;
            _isLeaving = true;

            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            if (_moveIdlePublisher != null)
                _moveIdlePublisher.Publish(new MoveIdleEvent());
            else if (_logger != null)
                _logger.ZLogWarning($"[OutroSceneManager] MoveIdleEvent 발행자가 주입되지 않아 관람 완료 집계를 보내지 못했습니다.");
            SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
        }
    }
}
