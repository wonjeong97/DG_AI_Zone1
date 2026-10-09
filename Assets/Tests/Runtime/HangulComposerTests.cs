using Admin;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 관리자 페이지 이름 입력 키보드의 한글 조합 규칙과 최대 글자 수 처리를 검증한다.
    /// TryPush는 길이가 넘치면 입력 전 상태로 되돌려야 하는데, 되돌리기가 어긋나면 글자가 사라지거나
    /// 조합 상태가 꼬인 채 남아 이후 입력이 전부 이상해진다.
    /// </summary>
    public class HangulComposerTests
    {
        private const int MaxLength = 5;

        private HangulComposer _composer;

        /// <summary>
        /// 빈 조합기를 새로 만든다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _composer = new HangulComposer();
        }

        /// <summary>
        /// 초성/중성/종성이 하나의 완성형 음절로 합쳐져야 한다.
        /// </summary>
        [Test]
        public void 초성_중성_종성이_한_음절로_조합된다()
        {
            Push("ㄱㅏㄴ");

            Assert.AreEqual("간", _composer.Text);
        }

        /// <summary>
        /// 종성이 있는 음절 뒤에 모음이 오면 그 종성이 다음 음절의 초성으로 넘어가야 한다.
        /// </summary>
        [Test]
        public void 겹받침_뒤에_모음이_오면_뒤쪽_받침이_다음_음절로_넘어간다()
        {
            Push("ㄱㅏㅂㅅ");
            Assert.AreEqual("값", _composer.Text, "겹받침이 만들어지지 않음");

            _composer.Push('ㅏ');
            Assert.AreEqual("갑사", _composer.Text, "겹받침의 뒤쪽만 다음 음절 초성으로 넘어가야 함");
        }

        /// <summary>
        /// 최대 글자 수를 넘기는 입력은 무시되고, 무시된 뒤에도 이전 상태가 온전해야 한다.
        /// </summary>
        [Test]
        public void 최대_글자수를_넘기는_입력은_무시되고_이전_상태가_보존된다()
        {
            Push("ㄱㄴㄷㄹㅁ");
            Assert.AreEqual("ㄱㄴㄷㄹㅁ", _composer.Text);

            bool accepted = _composer.TryPush('ㅂ', MaxLength);

            Assert.IsFalse(accepted, "최대 글자 수를 넘겼는데도 입력이 받아들여짐");
            Assert.AreEqual("ㄱㄴㄷㄹㅁ", _composer.Text, "거부된 입력이 확정 텍스트를 건드림");
        }

        /// <summary>
        /// 이미 최대 글자 수에 도달했어도 길이가 늘지 않는 입력(받침 붙이기)은 허용되어야 한다.
        /// </summary>
        [Test]
        public void 최대_글자수에_도달해도_길이가_늘지_않는_받침은_허용된다()
        {
            Push("ㄱㄴㄷㄹㄱㅏ");
            Assert.AreEqual("ㄱㄴㄷㄹ가", _composer.Text);

            bool accepted = _composer.TryPush('ㄴ', MaxLength);

            Assert.IsTrue(accepted, "길이가 늘지 않는 받침 입력이 거부됨");
            Assert.AreEqual("ㄱㄴㄷㄹ간", _composer.Text);
        }

        /// <summary>
        /// 거부된 입력 뒤에도 조합이 계속 정상 동작해야 한다(상태가 꼬이지 않았는지 확인).
        /// </summary>
        [Test]
        public void 거부된_입력_뒤에도_조합이_계속_정상_동작한다()
        {
            Push("ㄱㅏㄴㄷㅏ");
            Assert.AreEqual("간다", _composer.Text);

            // 모음은 새 음절을 시작시키므로 길이가 늘어나 거부된다
            Assert.IsFalse(_composer.TryPush('ㅗ', 2), "이미 두 글자라 새 음절은 거부되어야 함");
            Assert.AreEqual("간다", _composer.Text);

            Assert.IsTrue(_composer.TryPush('ㅇ', 2), "받침은 길이가 늘지 않으므로 허용되어야 함");
            Assert.AreEqual("간당", _composer.Text);
        }

        /// <summary>
        /// Backspace는 종성 → 중성 → 초성 순으로 한 단계씩 되돌려야 한다.
        /// </summary>
        [Test]
        public void 백스페이스가_종성_중성_초성_순으로_되돌린다()
        {
            Push("ㄱㅏㄴ");

            _composer.Backspace();
            Assert.AreEqual("가", _composer.Text);

            _composer.Backspace();
            Assert.AreEqual("ㄱ", _composer.Text);

            _composer.Backspace();
            Assert.AreEqual(string.Empty, _composer.Text);
        }

        /// <summary>
        /// TryAppendRaw는 최대 글자 수에 도달하면 거부해야 한다.
        /// </summary>
        [Test]
        public void 최대_글자수에_도달하면_원문자_추가가_거부된다()
        {
            for (int i = 0; i < MaxLength; i++)
                Assert.IsTrue(_composer.TryAppendRaw('a', MaxLength));

            Assert.IsFalse(_composer.TryAppendRaw('b', MaxLength), "최대 글자 수를 넘겼는데도 받아들여짐");
            Assert.AreEqual("aaaaa", _composer.Text);
        }

        /// <summary>
        /// 조합 중인 음절이 있을 때 숫자(원문자)를 붙이면 먼저 확정되어야 한다 — 숫자열 키 입력.
        /// </summary>
        [Test]
        public void 조합_중인_음절은_숫자_추가_전에_확정된다()
        {
            Push("ㄱㅏ");
            _composer.AppendRaw('1');
            Push("ㄴㅏ");

            Assert.AreEqual("가1나", _composer.Text);
        }

        /// <summary>
        /// 테스트 문자열의 자모를 순서대로 밀어 넣는다.
        /// </summary>
        private void Push(string jamos)
        {
            foreach (char jamo in jamos)
                _composer.Push(jamo);
        }
    }
}
