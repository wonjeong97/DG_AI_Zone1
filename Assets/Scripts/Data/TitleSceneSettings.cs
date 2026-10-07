using System;

namespace Data
{
    // StreamingAssets/0_Title.json 매핑 — 0_Title 씬의 연출 타이밍을 재빌드 없이 조정
    [Serializable]
    public class TitleSceneSettings
    {
        public float qrFadeDuration = 1.2f;
        public float qrBlinkMinAlpha = 0.3f;

        // 서버 모드에서 'QR 코드를 확인하고 있습니다'를 보여 주는 최소 시간(초) — 서버가 빨리 답해도 문구가 깜빡이듯 스치지 않게
        public float qrCheckingMinSeconds = 1f;

        // 서버 모드에서 QR 확인이 안 됐을 때(체험 완료·없는 QR·서버 오류) 안내를 보여 준 뒤 다시 QR을 기다리기까지의 시간(초)
        public float scanResultMessageSeconds = 3f;
    }
}
