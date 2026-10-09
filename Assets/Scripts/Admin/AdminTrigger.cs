using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Admin
{
    // 화면 구석의 보이지 않는 버튼 — 제한 시간 안에 정해진 횟수만큼 연속으로 누르면 관리자 비밀번호 창을 연다.
    // 횟수·시간은 Admin.json(entryClickCount·entryClickWindowSeconds)에서 읽고, 읽기 전에는 기본값(10회·3초)을 쓴다.
    // 템플릿 GameCloser(앱 종료)와 같은 원리이며, 겹치지 않도록 반대쪽 구석에 둔다.
    // 관리자 레벨 이동으로 플레이하고 타이틀에 돌아온 경우에는 비밀번호 없이 관리자 화면을 바로 다시 연다.
    [RequireComponent(typeof(Button))]
    public class AdminTrigger : MonoBehaviour
    {
        [SerializeField] private AdminPasswordPanel passwordPanel;
        [Tooltip("관리자 레벨 이동에서 돌아왔을 때 바로 열 관리자 화면")]
        [SerializeField] private AdminPanel adminPanel;

        private Button _button;
        private ConsecutiveClickCounter _counter;
        private int _targetClickCount = Constants.Admin.DefaultEntryClickCount;
        private ILogger<AdminTrigger> _logger;
        private GameSession _session;

        /// <summary>
        /// 로거와 게임 세션을 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<AdminTrigger> logger, GameSession session)
        {
            _logger = logger;
            _session = session;
        }

        /// <summary>
        /// 클릭 카운터를 만들고 숨은 버튼에 클릭 동작을 연결한다.
        /// </summary>
        private void Awake()
        {
            _counter = new ConsecutiveClickCounter(_targetClickCount, Constants.Admin.DefaultEntryClickWindowSeconds);
            if (TryGetComponent(out _button))
                _button.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// 연결 누락을 경고하고(씬 주입은 Awake 뒤라 로그를 남기도록 Start에서 확인), Admin.json의 클릭 횟수·시간을 읽고,
        /// 관리자 레벨 이동에서 돌아온 경우 관리자 화면을 다시 연다.
        /// </summary>
        private void Start()
        {
            if (!_button && _logger != null) _logger.ZLogWarning($"[AdminTrigger] {name}에 Button이 없어 관리자 진입을 받을 수 없습니다.");
            if (!passwordPanel && _logger != null) _logger.ZLogWarning($"[AdminTrigger] passwordPanel이 할당되지 않았습니다.");
            LoadClickSettingsAsync(destroyCancellationToken).Forget();

            if (_session == null || !_session.openAdminOnTitle) return;

            _session.openAdminOnTitle = false;
            if (adminPanel) adminPanel.Open();
            else if (_logger != null) _logger.ZLogWarning($"[AdminTrigger] adminPanel이 할당되지 않아 관리자 화면을 다시 열 수 없습니다.");
        }

        /// <summary>
        /// Admin.json의 연속 클릭 횟수·시간으로 클릭 카운터를 다시 만든다.
        /// </summary>
        private async UniTaskVoid LoadClickSettingsAsync(CancellationToken ct)
        {
            try
            {
                AdminSettings settings = await AdminSettings.LoadAsync(ct, _logger);
                _targetClickCount = settings.entryClickCount;
                _counter = new ConsecutiveClickCounter(settings.entryClickCount, settings.entryClickWindowSeconds);
            }
            catch (OperationCanceledException)
            {
                // 읽는 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 버튼 클릭 연결을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>
        /// 클릭을 세고, 목표 횟수에 닿으면 비밀번호 창을 연다.
        /// </summary>
        private void OnClicked()
        {
            if (!_counter.Register(Time.unscaledTime)) return;

            if (passwordPanel)
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminTrigger] {_targetClickCount}회 연속 클릭 — 관리자 비밀번호 창을 엽니다.");
                passwordPanel.Open();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[AdminTrigger] passwordPanel이 할당되지 않아 비밀번호 창을 열 수 없습니다.");
            }
        }
    }
}
