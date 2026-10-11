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

        // 아두이노가 받은 명령을 그대로 돌려주기를 기다리는 시간(초) — 이 안에 오지 않으면 같은 명령을 다시 보낸다
        public float replyTimeoutSeconds = Constants.Arduino.DefaultReplyTimeoutSeconds;

        // 응답이 없을 때 최대 보내는 횟수(첫 전송 포함) — 아두이노가 꺼져 있어도 끝없이 보내지 않게 한다
        public int maxSendAttempts = Constants.Arduino.DefaultMaxSendAttempts;

        /// <summary>
        /// 0 이하·숫자가 아닌 응답 대기 시간과 1보다 작은 보내는 횟수는 기본값으로, 너무 긴 대기 시간은 최댓값으로 바꾸고, 바꾼 값이 있으면 true를 돌려준다.
        /// </summary>
        public bool ClampToValid()
        {
            bool changed = false;

            // 0이면 아두이노가 답하기도 전에 다시 보내 같은 명령이 여러 번 실행된다
            replyTimeoutSeconds = SettingsClamp.Positive(replyTimeoutSeconds, Constants.Arduino.DefaultReplyTimeoutSeconds, ref changed);
            if (replyTimeoutSeconds > Constants.Arduino.MaxReplyTimeoutSeconds)
            {
                replyTimeoutSeconds = Constants.Arduino.MaxReplyTimeoutSeconds;
                changed = true;
            }

            maxSendAttempts = SettingsClamp.Positive(maxSendAttempts, Constants.Arduino.DefaultMaxSendAttempts, ref changed);
            return changed;
        }
    }
}
