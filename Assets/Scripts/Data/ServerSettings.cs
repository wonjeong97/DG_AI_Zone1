using System;

namespace Data
{
    // StreamingAssets/Json/Server.json 매핑 — 체험자 서버 주소, 응답 대기 시간, 요청 실패 시 재시도.
    // 서버는 현장 내부망에 있어 주소가 바뀌면 재빌드 없이 파일만 고친다.
    [Serializable]
    public class ServerSettings
    {
        // 예: http://192.168.0.52:8500 — 끝의 '/'는 있어도 된다
        public string baseUrl = string.Empty;

        // 결과 업로드(updateValue)의 응답 대기 시간(초)과, 연결 실패·시간 초과·HTTP 오류일 때 최대 시도 횟수(첫 시도 포함)
        public int uploadTimeoutSeconds = Constants.VisitorApi.DefaultUploadTimeoutSeconds;
        public int uploadMaxAttempts = Constants.VisitorApi.DefaultUploadMaxAttempts;

        // 타이틀 QR 확인(checkActive·getUser)용 — 체험자가 화면 앞에서 기다리므로 따로 짧게 둔다
        public int qrCheckTimeoutSeconds = Constants.VisitorApi.DefaultQrCheckTimeoutSeconds;
        public int qrCheckMaxAttempts = Constants.VisitorApi.DefaultQrCheckMaxAttempts;

        // 재시도 간격(초) — 모든 요청 공통
        public float retryDelaySeconds = Constants.VisitorApi.DefaultRetryDelaySeconds;
    }
}
