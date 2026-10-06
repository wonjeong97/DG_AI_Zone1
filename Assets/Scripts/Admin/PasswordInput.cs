using System.Text;

namespace Admin
{
    // 키패드로 입력 중인 숫자 비밀번호 — 최대 자릿수까지만 받고, 지우기·확인 판정을 맡는다.
    public class PasswordInput
    {
        private readonly StringBuilder _digits = new(Constants.Admin.PasswordMaxLength);

        public int Length => _digits.Length;

        // 확인을 누를 수 있는 길이(4~6자리)인지
        public bool HasValidLength => Length >= Constants.Admin.PasswordMinLength;

        /// <summary>
        /// 숫자 한 자리를 덧붙인다. 0~9가 아니거나 이미 최대 자릿수면 무시하고 false를 돌려준다.
        /// </summary>
        public bool TryAppend(int digit)
        {
            if (digit < 0 || digit > 9 || Length >= Constants.Admin.PasswordMaxLength) return false;

            _digits.Append((char)('0' + digit));
            return true;
        }

        /// <summary>
        /// 마지막 자리를 지운다. 비어 있으면 아무것도 하지 않는다.
        /// </summary>
        public void RemoveLast()
        {
            if (Length > 0) _digits.Length--;
        }

        /// <summary>
        /// 입력을 모두 지운다.
        /// </summary>
        public void Clear()
        {
            _digits.Clear();
        }

        /// <summary>
        /// 입력한 숫자가 비밀번호와 글자까지 같은지 확인한다 (문자열을 새로 만들지 않고 비교).
        /// </summary>
        public bool Matches(string password)
        {
            if (password == null || password.Length != Length) return false;

            for (int i = 0; i < Length; i++)
                if (_digits[i] != password[i]) return false;
            return true;
        }

        /// <summary>
        /// 입력한 숫자를 문자열로 돌려준다 (새 비밀번호를 저장할 때 쓴다).
        /// </summary>
        public override string ToString()
        {
            return _digits.ToString();
        }

        /// <summary>
        /// 저장된 비밀번호가 키패드로 입력할 수 있는 값(숫자 4~6자리)인지 확인한다.
        /// </summary>
        public static bool IsValidPassword(string value)
        {
            if (value == null
                || value.Length < Constants.Admin.PasswordMinLength
                || value.Length > Constants.Admin.PasswordMaxLength) return false;

            foreach (char c in value)
                if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
