using Network;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 레벨 결과를 서버에 올릴 때 쓰는 이 존(1존)의 콘텐츠 코드와 저장 응답 판정을 검증한다.
    /// </summary>
    public class VisitorApiClientTests
    {
        /// <summary>
        /// 레벨 순번(0부터)이 A1~A5로 바뀐다 — 레벨1은 A1, 레벨5는 A5.
        /// </summary>
        [TestCase(0, "A1")]
        [TestCase(1, "A2")]
        [TestCase(2, "A3")]
        [TestCase(3, "A4")]
        [TestCase(4, "A5")]
        public void 레벨_순번은_1존_콘텐츠_코드로_바뀐다(int levelIndex, string expected)
        {
            Assert.AreEqual(expected, VisitorApiClient.GetLevelCode(levelIndex));
        }

        /// <summary>
        /// updateValue 응답의 result가 true면 저장 성공이다 — 응답 예시는 현장 서버 실측값이다.
        /// </summary>
        [TestCase("{\n    \"result\":true,\n    \"idx_user\":8,\n    \"code\":\"A1\",\n    \"value\":0\n}")]
        [TestCase("\r\n{\"result\":true,\"idx_user\":10,\"code\":\"A5\",\"value\":1}\r\n")]
        public void 결과_저장_응답의_result가_true면_성공이다(string body)
        {
            Assert.IsTrue(UpdateValueResponse.IsSaved(body));
        }

        /// <summary>
        /// result가 false이거나 JSON이 아닌 응답은 저장 실패로 본다.
        /// </summary>
        [TestCase("{\"result\":false,\"message\":\"ERROR_IDX_USER\"}")]
        [TestCase("{\"result\":false,\"message\":\"NOT_FOUND\"}")]
        [TestCase("{}")]
        [TestCase("OK")]
        [TestCase("<html><body>Error</body></html>")]
        [TestCase("")]
        [TestCase(null)]
        public void 결과_저장_실패_응답은_실패로_본다(string body)
        {
            Assert.IsFalse(UpdateValueResponse.IsSaved(body));
        }
    }
}
