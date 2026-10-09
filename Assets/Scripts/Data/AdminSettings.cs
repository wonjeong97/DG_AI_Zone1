using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuliacDev.Utils;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Data
{
    // StreamingAssets/Json/Admin.json 매핑 — 관리자 페이지 비밀번호(숫자 4~6자리)와 관리자 창 시간·진입 클릭 수.
    // 현장에서 파일을 고쳐 바꾸거나, 잊어버렸을 때 기본값으로 되돌릴 수 있게 JSON에 둔다.
    // 키가 빠진 예전 파일도 읽을 수 있도록 모든 값에 기본값을 둔다.
    [Serializable]
    public class AdminSettings
    {
        public string password = Constants.Admin.DefaultPassword;

        // 관리자 화면·체험자 이름 입력 창을 입력 없이 두면 저장하지 않고 닫는 시간(초) — 열어 둔 채 자리를 뜨면 관람객이 설정을 바꿀 수 있다
        public float idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds;

        // 비밀번호 창을 입력 없이 두면 닫는 시간(초)
        public float passwordIdleCloseSeconds = Constants.Admin.DefaultPasswordIdleCloseSeconds;

        // 타이틀 좌상단 숨은 버튼을 entryClickWindowSeconds초 안에 entryClickCount번 누르면 비밀번호 창을 연다
        public int entryClickCount = Constants.Admin.DefaultEntryClickCount;
        public float entryClickWindowSeconds = Constants.Admin.DefaultEntryClickWindowSeconds;

        /// <summary>
        /// Admin.json을 읽는다. 시간·횟수가 1보다 작으면 경고를 남기고 기본값으로 바꾼다
        /// (비밀번호 검사는 키패드 규칙을 아는 AdminPasswordPanel이 한다).
        /// </summary>
        public static async UniTask<AdminSettings> LoadAsync(CancellationToken ct, ILogger logger)
        {
            AdminSettings settings = await JsonLoader.LoadAsync<AdminSettings>(Constants.SettingsFiles.Admin, ct, logger);
            if (settings.ClampToValid() && logger != null)
                logger.ZLogWarning($"[AdminSettings] Admin.json의 시간·횟수 값이 1보다 작아 그 값은 기본값을 씁니다.");
            return settings;
        }

        /// <summary>
        /// 1보다 작은 시간·횟수를 기본값으로 바꾼다 — 창이 열리자마자 닫히거나 숨은 버튼 진입이 동작하지 않는 일을 막는다.
        /// 바꾼 값이 있으면 true를 돌려준다.
        /// </summary>
        public bool ClampToValid()
        {
            bool changed = false;
            if (idleCloseSeconds < 1f) { idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds; changed = true; }
            if (passwordIdleCloseSeconds < 1f) { passwordIdleCloseSeconds = Constants.Admin.DefaultPasswordIdleCloseSeconds; changed = true; }
            if (entryClickCount < 1) { entryClickCount = Constants.Admin.DefaultEntryClickCount; changed = true; }
            if (entryClickWindowSeconds < 1f) { entryClickWindowSeconds = Constants.Admin.DefaultEntryClickWindowSeconds; changed = true; }
            return changed;
        }
    }
}
