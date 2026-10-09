using Cysharp.Text;
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
        // 인트로·아웃트로 문구에서 체험자 이름으로 바꿀 자리
        public const string NamePlaceholder = "{name}";

        private const int NoVisitorIdx = -1;

        // 행동 로그 주어를 정할 수 없을 때(provider 없음) 쓰는 주어
        private const string UnknownLogSubject = "체험자가";

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
        /// 화면에 표시할 체험자 이름을 반환한다 — 서버 모드면 QR로 확인한 서버 이름, 아니면 VisitorSettings의 이름이다.
        /// </summary>
        public string GetName() => ResolveName(true);

        // 이름은 GetName과 같은 규칙으로 정하되,
        // 기본 이름으로 바꿀 때의 경고는 화면에 이름을 띄울 때 GetName이 이미 남기므로 행동 로그마다 되풀이하지 않는다.
        /// <summary>
        /// 행동 로그의 주어(예: "홍길동이", "김철수가")를 반환한다.
        /// </summary>
        public string LogSubject => AppendSubjectParticle(ResolveName(false));

        /// <summary>
        /// 행동 로그 주어를 provider 없이도 얻으며, provider가 null이면(주입 전·테스트에서 만든 오브젝트) "체험자가"를 쓴다.
        /// </summary>
        public static string LogSubjectOf(VisitorInfoProvider provider) => provider != null ? provider.LogSubject : UnknownLogSubject;

        /// <summary>
        /// word 뒤에 받침 유무에 맞는 주격 조사를 붙인다(예: 홍길동이, 김철수가).
        /// </summary>
        public static string AppendSubjectParticle(string word)
        {
            if (string.IsNullOrEmpty(word)) return UnknownLogSubject;

            char last = word[word.Length - 1];
            // 마지막 글자가 한글 음절이 아니면(영문 이니셜 등) "이(가)"를 붙인다.
            if (last < '가' || last > '힣') return ZString.Concat(word, "이(가)");

            bool hasFinalConsonant = (last - '가') % 28 != 0;
            return ZString.Concat(word, hasFinalConsonant ? "이" : "가");
        }

        /// <summary>
        /// GetName의 이름 규칙으로 이름을 정한다.
        /// </summary>
        private string ResolveName(bool warnOnFallback)
        {
            // warnOnFallback이면 서버 모드인데 확인한 이름이 없어 기본 이름으로 바꿀 때 경고를 남긴다.
            if (_settings.IsServerConnected)
            {
                if (!string.IsNullOrEmpty(ServerVisitorName)) return ServerVisitorName;

                // 관리자 화면의 레벨 이동처럼 QR 확인 없이 시작한 판이거나 서버 이름이 비어 있는 경우
                if (warnOnFallback && _logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] 서버 모드이지만 QR로 확인한 체험자 이름이 없어 기본 이름으로 대체합니다.");
            }

            string visitorName = _settings.VisitorName;
            return string.IsNullOrEmpty(visitorName) ? Constants.Admin.DefaultVisitorName : visitorName;
        }

        /// <summary>
        /// 문구의 "{name}" 자리를 지금 체험자 이름으로 바꾼 문자열을 반환한다.
        /// </summary>
        public string FillName(string template)
        {
            if (string.IsNullOrEmpty(template)) return template;

            using Utf16ValueStringBuilder sb = ZString.CreateStringBuilder();
            sb.Append(template);
            sb.Replace(NamePlaceholder, GetName());
            return sb.ToString();
        }
    }
}
