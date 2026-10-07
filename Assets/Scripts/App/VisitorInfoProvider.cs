using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace App
{
    // 운영 모드(서버 연동 여부)에 따라 체험자 이름을 결정한다.
    // 모드와 로컬 모드 이름은 VisitorSettings(SO, 관리자 페이지에서 변경)에서 읽는다.
    // 서버 모드에서는 타이틀이 QR로 확인한 체험자(서버의 idx_user·이름)를 들고 있다가 타이틀로 돌아오면 비운다.
    public class VisitorInfoProvider
    {
        private const string FallbackName = "체험자";
        private const int NoVisitorIdx = -1;

        private readonly VisitorSettings _settings;
        private readonly ILogger<VisitorInfoProvider> _logger;

        // 서버 모드에서 QR로 확인한 체험자의 idx_user — 확인 전이거나 체험이 끝나면 -1
        public int VisitorIdx { get; private set; } = NoVisitorIdx;

        // 서버 모드에서 QR로 확인한 체험자의 서버 이름(영문 이니셜) — 확인 전이거나 체험이 끝나면 null
        public string ServerVisitorName { get; private set; }

        /// <summary>
        /// 체험자 설정과 로거를 생성자 주입으로 받는다.
        /// </summary>
        public VisitorInfoProvider(VisitorSettings settings, ILogger<VisitorInfoProvider> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        // 서버 연동(QR 인식) 모드인지
        public bool IsServerConnected => _settings.IsServerConnected;

        /// <summary>
        /// 타이틀에서 QR로 확인한 서버 체험자를 기록한다.
        /// </summary>
        public void SetServerVisitor(int idxUser, string visitorName)
        {
            VisitorIdx = idxUser;
            ServerVisitorName = visitorName;
        }

        /// <summary>
        /// 서버 체험자 기록을 비운다 — 타이틀로 돌아오면 다음 체험자를 QR로 다시 확인해야 한다.
        /// </summary>
        public void ClearServerVisitor()
        {
            VisitorIdx = NoVisitorIdx;
            ServerVisitorName = null;
        }

        /// <summary>
        /// 화면에 표시할 체험자 이름을 반환한다. 서버 모드면 QR로 확인한 서버 이름, 아니면 VisitorSettings의 이름이다.
        /// </summary>
        public UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            if (_settings.IsServerConnected)
            {
                if (!string.IsNullOrEmpty(ServerVisitorName)) return UniTask.FromResult(ServerVisitorName);

                // 관리자 화면의 레벨 이동처럼 QR 확인 없이 시작한 판이거나 서버 이름이 비어 있는 경우
                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] 서버 모드이지만 QR로 확인한 체험자 이름이 없어 기본 이름으로 대체합니다.");
            }

            string visitorName = _settings.VisitorName;
            return UniTask.FromResult(string.IsNullOrEmpty(visitorName) ? FallbackName : visitorName);
        }
    }
}
