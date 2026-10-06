using System;

namespace Data
{
    // StreamingAssets/Json/Admin.json 매핑 — 관리자 페이지 비밀번호(숫자 4~6자리).
    // 현장에서 파일을 고쳐 바꾸거나, 잊어버렸을 때 기본값으로 되돌릴 수 있게 JSON에 둔다.
    [Serializable]
    public class AdminSettings
    {
        public string password = Constants.Admin.DefaultPassword;
    }
}
