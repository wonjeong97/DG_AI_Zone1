using Admin;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 관리자 페이지 진입 규칙 — 연속 클릭 판정과 키패드 비밀번호 입력을 검증한다.
    /// </summary>
    public class AdminLogicTests
    {
        /// <summary>
        /// 제한 시간 안에 목표 횟수를 채우면 마지막 클릭에서만 true가 되고 카운트가 비워진다.
        /// </summary>
        [Test]
        public void 제한_시간_안에_목표_횟수를_채우면_열린다()
        {
            ConsecutiveClickCounter counter = new ConsecutiveClickCounter(10, 3f);

            for (int i = 0; i < 9; i++)
                Assert.IsFalse(counter.Register(i * 0.2f), $"{i + 1}번째 클릭에서 열리면 안 됨");

            Assert.IsTrue(counter.Register(1.8f), "10번째 클릭에서 열려야 함");
            Assert.AreEqual(0, counter.Count, "연 뒤에는 다음 연속 클릭을 위해 카운트가 비워져야 함");
        }

        /// <summary>
        /// 첫 클릭부터 제한 시간이 지나면 지금 클릭을 새 1회로 센다.
        /// </summary>
        [Test]
        public void 제한_시간이_지나면_다시_1회부터_센다()
        {
            ConsecutiveClickCounter counter = new ConsecutiveClickCounter(10, 3f);

            for (int i = 0; i < 9; i++)
                counter.Register(i * 0.3f);

            Assert.IsFalse(counter.Register(3.5f), "첫 클릭에서 3초가 지난 10번째 클릭은 인정되지 않아야 함");
            Assert.AreEqual(1, counter.Count);
        }

        /// <summary>
        /// 최대 6자리까지만 입력되고, 지우기는 마지막 자리부터 지운다.
        /// </summary>
        [Test]
        public void 최대_자릿수까지만_입력되고_지우기는_마지막_자리를_지운다()
        {
            PasswordInput input = new PasswordInput();

            for (int i = 1; i <= Constants.Admin.PasswordMaxLength; i++)
                Assert.IsTrue(input.TryAppend(i), $"{i}번째 자리는 입력돼야 함");
            Assert.IsFalse(input.TryAppend(7), "최대 자릿수를 넘는 입력은 무시돼야 함");
            Assert.IsTrue(input.Matches("123456"));

            input.RemoveLast();
            Assert.IsTrue(input.Matches("12345"));

            input.Clear();
            input.RemoveLast();
            Assert.AreEqual(0, input.Length, "비어 있을 때 지우기는 아무것도 하지 않아야 함");
        }

        /// <summary>
        /// 4자리 미만은 확인할 수 없고, 비밀번호와 글자까지 같아야 맞다.
        /// </summary>
        [Test]
        public void 네_자리부터_확인할_수_있고_글자까지_같아야_맞다()
        {
            PasswordInput input = new PasswordInput();
            input.TryAppend(0);
            input.TryAppend(0);
            input.TryAppend(0);
            Assert.IsFalse(input.HasValidLength, "3자리는 확인할 수 없어야 함");

            input.TryAppend(0);
            Assert.IsTrue(input.HasValidLength);
            Assert.IsTrue(input.Matches(Constants.Admin.DefaultPassword));
            Assert.IsFalse(input.Matches("00000"), "길이가 다르면 틀려야 함");
            Assert.IsFalse(input.Matches("0001"));
            Assert.IsFalse(input.Matches(null));
        }

        /// <summary>
        /// 저장된 비밀번호는 숫자 4~6자리만 유효하다.
        /// </summary>
        [Test]
        public void 저장된_비밀번호는_숫자_4에서_6자리만_유효하다()
        {
            Assert.IsTrue(PasswordInput.IsValidPassword("0000"));
            Assert.IsTrue(PasswordInput.IsValidPassword("123456"));
            Assert.IsFalse(PasswordInput.IsValidPassword("123"));
            Assert.IsFalse(PasswordInput.IsValidPassword("1234567"));
            Assert.IsFalse(PasswordInput.IsValidPassword("12a4"));
            Assert.IsFalse(PasswordInput.IsValidPassword(" 1234"));
            Assert.IsFalse(PasswordInput.IsValidPassword(""));
            Assert.IsFalse(PasswordInput.IsValidPassword(null));
        }
    }
}
