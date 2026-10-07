using NUnit.Framework;
using Scenes;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 타이틀 QR 스캐너 입력 모음을 검증한다 — 스캐너는 uid를 한 번에 빠르게 보내므로, 찍기 전에 눌린 키처럼
    /// 0.5초 넘게 떨어진 앞 글자는 uid에 붙지 않아야 한다(실제 리더기 테스트에서 한 글자가 붙어 13자로 들어온 경우가 있었다).
    /// </summary>
    public class ScanInputBufferTests
    {
        /// <summary>
        /// 간격이 짧은 글자는 그대로 이어 붙는다.
        /// </summary>
        [Test]
        public void 간격이_짧은_글자는_이어_붙는다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            float t = 10f;
            foreach (char c in "440930W1XWQH")
            {
                Assert.AreEqual(0, buffer.Append(c, t), "스캔 도중 글자가 버려지면 안 됨");
                t += 0.02f;
            }

            Assert.IsFalse(buffer.IsStale(t));
            Assert.AreEqual("440930W1XWQH", buffer.TakeAndClear());
            Assert.AreEqual(0, buffer.Length, "꺼낸 뒤에는 비어 있어야 함");
        }

        /// <summary>
        /// 0.5초 넘게 떨어진 글자가 오면 앞에 모은 글자를 버리고 버린 개수를 돌려준다.
        /// </summary>
        [Test]
        public void 간격이_0점5초를_넘으면_앞_글자를_버린다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Append('a', 1f);

            float t = 1f + ScanInputBuffer.DefaultMaxCharGapSeconds + 0.1f;
            Assert.AreEqual(1, buffer.Append('4', t), "찍기 전에 눌린 키 한 글자를 버려야 함");
            foreach (char c in "40930W1XWQH")
            {
                t += 0.02f;
                buffer.Append(c, t);
            }

            Assert.AreEqual("440930W1XWQH", buffer.TakeAndClear());
        }

        /// <summary>
        /// 간격이 정확히 0.5초면 같은 스캔으로 본다(넘을 때만 버린다).
        /// </summary>
        [Test]
        public void 간격이_정확히_0점5초면_버리지_않는다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Append('A', 2f);

            Assert.AreEqual(0, buffer.Append('B', 2f + ScanInputBuffer.DefaultMaxCharGapSeconds));
            Assert.AreEqual("AB", buffer.TakeAndClear());
        }

        /// <summary>
        /// 마지막 글자보다 0.5초 넘게 지나면 모은 글자는 낡은 입력이고, 비어 있으면 낡지 않았다.
        /// </summary>
        [Test]
        public void 마지막_글자_뒤로_0점5초가_지나면_낡은_입력이다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            Assert.IsFalse(buffer.IsStale(100f), "빈 버퍼는 낡은 입력이 아님");

            buffer.Append('x', 5f);
            Assert.IsFalse(buffer.IsStale(5f + ScanInputBuffer.DefaultMaxCharGapSeconds));
            Assert.IsTrue(buffer.IsStale(5f + ScanInputBuffer.DefaultMaxCharGapSeconds + 0.01f));

            buffer.Clear();
            Assert.IsFalse(buffer.IsStale(100f), "비운 뒤에는 낡은 입력이 아님");
        }

        /// <summary>
        /// 간격을 늘리면(0_Title.json scanCharGapSeconds — PC가 느린 현장) 기본값보다 늦게 온 글자도 같은 스캔으로 본다.
        /// </summary>
        [Test]
        public void 간격을_늘리면_늦게_온_글자도_이어_붙는다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer { MaxCharGapSeconds = 1.5f };
            buffer.Append('4', 0f);

            Assert.AreEqual(0, buffer.Append('4', 1.2f), "늘린 간격 안이면 버리면 안 됨");
            Assert.IsFalse(buffer.IsStale(2.7f));
            Assert.AreEqual(2, buffer.Append('0', 2.8f), "늘린 간격도 넘으면 버려야 함");
            Assert.AreEqual("0", buffer.TakeAndClear());
        }
    }
}
