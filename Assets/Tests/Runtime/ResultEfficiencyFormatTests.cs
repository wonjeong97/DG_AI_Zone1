using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 결과 화면 에너지 효율 숫자가 한 자리 수도 앞에 0을 붙이지 않고 보이는지 검증한다(02%가 아니라 2%).
    /// </summary>
    public class ResultEfficiencyFormatTests
    {
        /// <summary>
        /// 효율 퍼센트는 숫자 그대로에 %만 붙는다.
        /// </summary>
        [TestCase(0, "0%")]
        [TestCase(2, "2%")]
        [TestCase(45, "45%")]
        [TestCase(100, "100%")]
        public void 효율은_앞에_0을_붙이지_않는다(int percent, string expected)
        {
            Assert.AreEqual(expected, string.Format(Constants.ResultMessages.EfficiencyFormat, percent));
        }
    }
}
