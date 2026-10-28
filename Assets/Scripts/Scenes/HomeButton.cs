using App;
using Cysharp.Threading.Tasks;
using Data;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Network;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.App;
using HuliacDev.UI;
using ZLogger;

namespace Scenes
{
    /// <summary>
    /// 화면 우상단 홈 버튼 — 누르면 서버에 move_idle을 보내고 타이틀로 돌아간다(인트로·레벨 선택 씬).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HomeButton : MonoBehaviour
    {
        private Button _button;
        private ILogger<HomeButton> _logger;
        private IPublisher<MoveIdleEvent> _moveIdlePublisher;
        private SoundManager _soundManager;
        private VisitorInfoProvider _visitorInfoProvider; // 행동 로그 주어
        private GameSession _session;
        private ArduinoUdpClient _arduino;

        // 이미 눌렀는지 — 연타해도 move_idle이 한 번만 나가도록
        private bool _isLeaving;

        /// <summary>
        /// 로거, 관람 종료 이벤트 발행자, 사운드 매니저, 체험자 정보, 게임 세션, 아두이노 명령 클라이언트를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<HomeButton> log, IPublisher<MoveIdleEvent> moveIdlePublisher, SoundManager soundManager,
            VisitorInfoProvider visitorInfoProvider, GameSession session, ArduinoUdpClient arduino)
        {
            _logger = log;
            _moveIdlePublisher = moveIdlePublisher;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
            _session = session;
            _arduino = arduino;
        }

        /// <summary>
        /// 버튼 클릭을 구독한다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[HomeButton] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            if (TryGetComponent(out _button)) _button.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// 버튼 리스너를 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>
        /// 화면 좌표가 이 버튼 위인지 돌려준다 — 화면 아무 곳 터치로 넘어가는 연출이 홈 버튼 터치까지 받지 않게 한다.
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            return isActiveAndEnabled && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screenPosition);
        }

        /// <summary>
        /// 체험자가 스스로 처음 화면으로 돌아가므로 서버 통계에 move_idle로 집계하고 타이틀로 간다.
        /// </summary>
        private void OnClicked()
        {
            // 씬 전환 중(뒤로·튜토리얼 끝 등으로 이미 떠나는 중)이면 move_idle을 보내지 않는다
            if (_isLeaving || SceneFader.IsLoading) return;
            _isLeaving = true;

            if (_logger != null) _logger.ZLogInformation($"[HomeButton] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 홈을 누름 — 타이틀로 돌아감.");
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);

            if (_moveIdlePublisher != null) _moveIdlePublisher.Publish(new MoveIdleEvent());
            else if (_logger != null) _logger.ZLogWarning($"[HomeButton] MoveIdleEvent 발행자가 주입되지 않아 move_idle을 보내지 못했습니다.");

            // 스토리 화면(레벨을 골라 enter를 보낸 뒤)에서 누르면 아두이노 조명·장치를 그 레벨 상태로 두지 않는다 — enter가 없으면 보내지 않는다
            if (_arduino != null) _arduino.SendExit();
            else if (_logger != null) _logger.ZLogWarning($"[HomeButton] ArduinoUdpClient가 주입되지 않아 아두이노에 exit를 보내지 못했습니다.");

            // 관리자 레벨 이동으로 시작한 판은 스토리의 < 버튼처럼 타이틀의 관리자 화면으로 돌아간다
            if (_session != null && _session.isAdminLevelJump) _session.openAdminOnTitle = true;

            SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
        }
    }
}
