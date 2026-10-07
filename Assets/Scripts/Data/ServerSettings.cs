using System;

namespace Data
{
    // StreamingAssets/Json/Server.json 매핑 — 체험자 서버 주소와 응답 대기 시간.
    // 서버는 현장 내부망에 있어 주소가 바뀌면 재빌드 없이 파일만 고친다.
    [Serializable]
    public class ServerSettings
    {
        // 예: http://192.168.0.52:8500 — 끝의 '/'는 있어도 된다
        public string baseUrl = string.Empty;
        public int timeoutSeconds = Constants.VisitorApi.DefaultTimeoutSeconds;
    }
}
