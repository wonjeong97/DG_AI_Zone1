using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace App
{
    // 운영 모드(서버 연동 여부)에 따라 체험자 이름을 결정한다.
    // 모드와 로컬 모드 이름은 VisitorSettings(SO, 관리자 페이지에서 변경)에서 읽는다.
    public class VisitorInfoProvider
    {
        private const string FallbackName = "체험자";

        private readonly VisitorSettings _settings;
        private readonly ILogger<VisitorInfoProvider> _logger;

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
        /// 화면에 표시할 체험자 이름을 반환한다 (서버 미연동 시 VisitorSettings의 이름).
        /// </summary>
        public UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            if (_settings.IsServerConnected)
            {
                // TODO: 서버 연동(예: QR 스캔)으로 실제 체험자 이름을 조회하도록 구현.
                // 서버 API가 준비되기 전까지는 기본 이름으로 대체함.
                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] 서버 모드이지만 서버 연동이 아직 구현되지 않아 기본 이름으로 대체합니다.");
            }

            string visitorName = _settings.VisitorName;
            return UniTask.FromResult(string.IsNullOrEmpty(visitorName) ? FallbackName : visitorName);
        }
    }
}
