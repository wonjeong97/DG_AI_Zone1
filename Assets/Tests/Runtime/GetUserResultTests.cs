using Network;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 체험자 서버 getUser 응답에서 이 존(1존, A1~A5)의 진행도를 읽는지 검증한다 — 응답 형식은 현장 서버 실측값이다.
    /// </summary>
    public class GetUserResultTests
    {
        // 현장 서버 실측 응답 — 기록이 없으면 null
        private const string AllNullBody = @"{
    ""result"": true,
    ""user"": {
        ""idx_user"": 10,
        ""name"": ""LLL"",
        ""uid"": ""440930W1XWQH"",
        ""dates"": ""2026-10-07 13:27:18"",

        ""A1"": null,
        ""A2"": null,
        ""A3"": null,
        ""A4"": null,
        ""A5"": null,

        ""B1"": null,
        ""B2"": null,
        ""B3"": null,
        ""B4"": null,
        ""B5"": null,

        ""C1"": null,
        ""C2"": null,
        ""C3"": null,
        ""C4"": null,
        ""C5"": null,

        ""D1"": null,
        ""D2"": null,
        ""D3"": null,
        ""D4"": null,
        ""D5"": null
    }
}";

        /// <summary>
        /// 이 존의 레벨 값만 바꾼 응답을 만든다.
        /// </summary>
        private static string WithValues(string a1, string a2, string a3, string a4, string a5) =>
            AllNullBody
                .Replace(@"""A1"": null", @"""A1"": " + a1)
                .Replace(@"""A2"": null", @"""A2"": " + a2)
                .Replace(@"""A3"": null", @"""A3"": " + a3)
                .Replace(@"""A4"": null", @"""A4"": " + a4)
                .Replace(@"""A5"": null", @"""A5"": " + a5);

        /// <summary>
        /// 기록이 하나도 없으면 찾았지만 마지막 기록 레벨은 -1이다(레벨1만 열림).
        /// </summary>
        [Test]
        public void 기록이_없으면_마지막_기록_레벨은_없다()
        {
            GetUserResult result = GetUserResult.Parse(AllNullBody);

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(-1, result.LastRecordedLevelIndex);
        }

        /// <summary>
        /// 성공(1)·실패(0) 모두 기록으로 보고, 값이 있는 마지막 레벨의 순번을 돌려준다.
        /// </summary>
        [TestCase("1", "null", "null", "null", "null", 0)]
        [TestCase("1", "0", "null", "null", "null", 1)]
        [TestCase("0", "null", "0", "null", "null", 2)]
        [TestCase("1", "1", "1", "1", "1", 4)]
        public void 값이_있는_마지막_레벨을_찾는다(string a1, string a2, string a3, string a4, string a5, int expected)
        {
            GetUserResult result = GetUserResult.Parse(WithValues(a1, a2, a3, a4, a5));

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(expected, result.LastRecordedLevelIndex);
        }

        /// <summary>
        /// 다른 존(B~D)의 기록은 이 존의 진행도에 영향을 주지 않는다.
        /// </summary>
        [Test]
        public void 다른_존의_기록은_무시한다()
        {
            string body = AllNullBody.Replace(@"""B1"": null", @"""B1"": 1").Replace(@"""D5"": null", @"""D5"": 0");

            Assert.AreEqual(-1, GetUserResult.Parse(body).LastRecordedLevelIndex);
        }

        /// <summary>
        /// 따옴표로 감싼 숫자("1"·"0")도 기록으로 본다.
        /// </summary>
        [Test]
        public void 따옴표로_감싼_숫자도_기록으로_본다()
        {
            GetUserResult result = GetUserResult.Parse(WithValues("\"1\"", "\"0\"", "null", "null", "null"));

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(1, result.LastRecordedLevelIndex);
        }

        /// <summary>
        /// 이 존에 없는 레벨 키(A0·A6·A10)는 무시해, 없는 레벨 기록 때문에 마지막 레벨까지 열리지 않는다.
        /// </summary>
        [Test]
        public void 이_존에_없는_레벨_키는_무시한다()
        {
            string body = WithValues("1", "null", "null", "null", "null")
                .Replace(@"""A5"": null", @"""A5"": null, ""A6"": 1, ""A10"": 0, ""A0"": 1");

            GetUserResult result = GetUserResult.Parse(body);

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(0, result.LastRecordedLevelIndex);
        }

        /// <summary>
        /// result가 false면 찾지 못한 것이고, 서버 메시지를 실패 사유로 남긴다.
        /// </summary>
        [Test]
        public void result가_false면_찾지_못한_것이다()
        {
            GetUserResult result = GetUserResult.Parse("{\"result\":false,\"message\":\"NOT_FOUND\"}");

            Assert.IsFalse(result.IsFound);
            Assert.AreEqual("NOT_FOUND", result.FailReason);
        }

        /// <summary>
        /// JSON이 아니거나 빈 응답은 찾지 못한 것으로 본다.
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("NOT_FOUND")]
        [TestCase("<html><body>Error</body></html>")]
        public void 약속하지_않은_응답은_찾지_못한_것이다(string body)
        {
            GetUserResult result = GetUserResult.Parse(body);

            Assert.IsFalse(result.IsFound);
            Assert.AreEqual(-1, result.LastRecordedLevelIndex);
        }
    }
}
