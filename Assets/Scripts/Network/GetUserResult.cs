using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// 체험자 서버 getUser 호출 결과 중 이 존(1존)의 진행도.
    /// 응답 예: {"result": true, "user": {"idx_user": 10, ..., "A1": null, ..., "D5": null}} — 값은 성공 1·실패 0·기록 없음 null.
    /// 없는 uid면 {"result":false,"message":"NOT_FOUND"}.
    /// </summary>
    public readonly struct GetUserResult
    {
        private const int NoRecord = -1;
        private const string NullValue = "null";

        // JsonUtility는 int 칸의 null과 0을 구분하지 못해 이 존의 레벨 값("A1": 1 등)은 정규식으로 직접 읽는다
        private readonly static Regex ZoneLevelPattern =
            new("\"" + Constants.VisitorApi.ZoneCode + "(\\d+)\"\\s*:\\s*(null|\"?\\d+\"?)", RegexOptions.Compiled);

        public bool IsFound { get; }

        // 기록(성공·실패)이 있는 이 존의 마지막 레벨 순번(0부터) — 기록이 없으면 -1
        public int LastRecordedLevelIndex { get; }

        // 진행도를 읽지 못한 이유 — 응답에 uid·이름이 있어 원문 대신 이것만 로그에 남긴다
        public string FailReason { get; }

        /// <summary>
        /// 찾음 여부, 마지막 기록 레벨 순번, 실패 사유로 결과를 만든다.
        /// </summary>
        private GetUserResult(bool isFound, int lastRecordedLevelIndex, string failReason)
        {
            IsFound = isFound;
            LastRecordedLevelIndex = lastRecordedLevelIndex;
            FailReason = failReason;
        }

        /// <summary>
        /// 진행도를 읽지 못한 결과를 만든다.
        /// </summary>
        public static GetUserResult Failed(string reason) => new(false, NoRecord, reason);

        /// <summary>
        /// getUser 응답 본문을 해석한다. result가 true일 때만 찾은 것으로 보고, 이 존의 레벨(A1~A5) 값 중 null이 아닌 마지막 레벨을 찾는다.
        /// </summary>
        public static GetUserResult Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return Failed("빈 응답");

            GetUserResponse response;
            try
            {
                response = JsonUtility.FromJson<GetUserResponse>(body);
            }
            catch (ArgumentException)
            {
                return Failed("JSON이 아닌 응답");
            }

            if (response == null) return Failed("빈 응답");
            if (!response.result) return Failed(string.IsNullOrEmpty(response.message) ? "result false" : response.message);

            int lastRecordedLevelIndex = NoRecord;
            foreach (Match match in ZoneLevelPattern.Matches(body))
            {
                if (match.Groups[2].Value == NullValue) continue;

                // 이 존의 레벨(A1~A5)만 본다 — 서버에 A6처럼 없는 레벨 키가 생겨도 마지막 레벨까지 열지 않게 한다
                if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int levelNumber)
                    && levelNumber >= 1 && levelNumber <= Constants.VisitorApi.LevelCount)
                    lastRecordedLevelIndex = Math.Max(lastRecordedLevelIndex, levelNumber - 1);
            }

            return new GetUserResult(true, lastRecordedLevelIndex, null);
        }

        // 응답 최상위의 성공 여부와 실패 메시지만 JsonUtility로 읽는다 — 필드 이름은 서버 JSON 키와 같아야 한다
        [Serializable]
        private class GetUserResponse
        {
            public bool result;
            public string message;
        }
    }
}
