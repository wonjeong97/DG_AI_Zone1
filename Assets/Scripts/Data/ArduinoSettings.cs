using System;

namespace Data
{
    // StreamingAssets/Json/Arduino.json 매핑 — 조명·장치를 움직이는 아두이노(1존 공용 1대)의 주소.
    // 아두이노는 현장 내부망에 랜선으로 연결되어 있어 주소가 바뀌면 재빌드 없이 파일만 고친다.
    [Serializable]
    public class ArduinoSettings
    {
        // IP 주소만 받는다(호스트 이름은 받지 않는다) — 예: 192.168.0.13
        public string host = string.Empty;

        // 아두이노가 명령을 받는 UDP 포트 — 예: 9000
        public int port;
    }
}
