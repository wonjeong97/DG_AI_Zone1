using Network;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 체험자 서버 checkActive 평문 응답을 해석하는지 검증한다 — 응답 예시는 현장 서버 실측값이다.
    /// </summary>
    public class CheckActiveResultTests
    {
        /// <summary>
        /// "idx_user,name"이면 체험 가능이고 idx와 이름을 읽는다.
        /// </summary>
        [Test]
        public void 숫자_쉼표_이름이면_체험_가능이다()
        {
            CheckActiveResult result = CheckActiveResult.Parse("10,LLL");

            Assert.AreEqual(CheckActiveStatus.Active, result.Status);
            Assert.AreEqual(10, result.IdxUser);
            Assert.AreEqual("LLL", result.Name);
        }

        /// <summary>
        /// .cfm 출력에 붙는 앞뒤 공백·줄바꿈은 무시한다.
        /// </summary>
        [Test]
        public void 앞뒤_공백과_줄바꿈은_무시한다()
        {
            CheckActiveResult result = CheckActiveResult.Parse("\r\n  10,LLL \r\n");

            Assert.AreEqual(CheckActiveStatus.Active, result.Status);
            Assert.AreEqual(10, result.IdxUser);
            Assert.AreEqual("LLL", result.Name);
        }

        /// <summary>
        /// 체험 완료 문구와 NOT_FOUND는 각각 완료·없음으로 나눈다.
        /// </summary>
        [TestCase("체험을 완료한 유저입니다", CheckActiveStatus.Completed)]
        [TestCase(" 체험을 완료한 유저입니다\n", CheckActiveStatus.Completed)]
        [TestCase("NOT_FOUND", CheckActiveStatus.NotFound)]
        [TestCase("NOT_FOUND\r\n", CheckActiveStatus.NotFound)]
        public void 약속한_문구는_완료와_없음으로_나눈다(string body, CheckActiveStatus expected)
        {
            Assert.AreEqual(expected, CheckActiveResult.Parse(body).Status);
        }

        /// <summary>
        /// 약속하지 않은 응답은 체험 가능으로 잘못 읽지 않고 Unknown으로 둔다.
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("LLL")]
        [TestCase("abc,LLL")]
        [TestCase(",LLL")]
        [TestCase("-1,LLL")]
        [TestCase("<html><body>Error</body></html>")]
        public void 약속하지_않은_응답은_Unknown이다(string body)
        {
            CheckActiveResult result = CheckActiveResult.Parse(body);

            Assert.AreEqual(CheckActiveStatus.Unknown, result.Status);
            Assert.AreEqual(-1, result.IdxUser);
        }

        /// <summary>
        /// 요청 실패 결과는 체험자 정보가 비어 있다.
        /// </summary>
        [Test]
        public void 요청_실패_결과는_체험자_정보가_없다()
        {
            CheckActiveResult result = CheckActiveResult.Failed();

            Assert.AreEqual(CheckActiveStatus.RequestFailed, result.Status);
            Assert.AreEqual(-1, result.IdxUser);
            Assert.IsNull(result.Name);
        }
    }
}
