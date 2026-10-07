using System.Globalization;

namespace Network
{
    // checkActive 응답을 해석한 체험 가능 여부
    public enum CheckActiveStatus
    {
        Active,        // 체험 가능 — IdxUser·Name이 채워진다
        Completed,     // 이미 체험을 완료한 uid
        NotFound,      // 서버에 없는 uid
        Unknown,       // 서버와 약속하지 않은 응답
        RequestFailed  // 서버 주소 미설정·연결 실패·시간 초과
    }

    /// <summary>
    /// 체험자 서버 checkActive 호출 결과. 응답은 JSON이 아닌 평문이다
    /// — 체험 가능 "idx_user,name"(예: "10,LLL"), 체험 완료 "체험을 완료한 유저입니다", 없는 uid "NOT_FOUND".
    /// </summary>
    public readonly struct CheckActiveResult
    {
        private const int NoIdxUser = -1;

        public CheckActiveStatus Status { get; }
        public int IdxUser { get; }
        public string Name { get; }

        /// <summary>
        /// 상태와 체험자 정보로 결과를 만든다.
        /// </summary>
        private CheckActiveResult(CheckActiveStatus status, int idxUser = NoIdxUser, string name = null)
        {
            Status = status;
            IdxUser = idxUser;
            Name = name;
        }

        /// <summary>
        /// 요청 자체가 실패한 결과를 만든다.
        /// </summary>
        public static CheckActiveResult Failed() => new(CheckActiveStatus.RequestFailed);

        /// <summary>
        /// checkActive 응답 본문을 해석한다. .cfm 출력의 앞뒤 공백·줄바꿈은 무시하고,
        /// 숫자 idx 뒤 첫 쉼표 이후를 이름으로 본다. 약속한 형식이 아니면 Unknown이다.
        /// </summary>
        public static CheckActiveResult Parse(string body)
        {
            string text = body == null ? string.Empty : body.Trim();

            if (text == Constants.VisitorApi.NotFoundResponse) return new(CheckActiveStatus.NotFound);
            if (text == Constants.VisitorApi.CompletedResponse) return new(CheckActiveStatus.Completed);

            int commaIndex = text.IndexOf(',');
            if (commaIndex > 0 &&
                int.TryParse(text.Substring(0, commaIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int idxUser))
                return new(CheckActiveStatus.Active, idxUser, text.Substring(commaIndex + 1).Trim());

            return new(CheckActiveStatus.Unknown);
        }
    }
}
