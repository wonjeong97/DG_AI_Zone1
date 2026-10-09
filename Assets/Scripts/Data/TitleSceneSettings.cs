using System;

namespace Data
{
    // StreamingAssets/Json/0_Title.json 매핑 — 0_Title 씬의 연출 타이밍과 하단 안내 문구를 재빌드 없이 조정
    [Serializable]
    public class TitleSceneSettings
    {
        public float qrFadeDuration = 1.2f;
        public float qrBlinkMinAlpha = 0.3f;

        // 서버 모드에서 'QR 코드를 확인하고 있습니다'를 보여 주는 최소 시간(초) — 서버가 빨리 답해도 문구가 깜빡이듯 스치지 않게
        public float qrCheckingMinSeconds = 1f;

        // 서버 모드에서 QR 확인이 안 됐을 때(체험 완료·없는 QR·서버 오류) 안내를 보여 준 뒤 다시 QR을 기다리기까지의 시간(초)
        public float scanResultMessageSeconds = 3f;

        // QR 스캐너 글자 사이 최대 간격(초) — 이보다 벌어지면 앞에 모은 글자(찍기 전에 눌린 키 등)를 버리고, Enter가 이보다 늦게 오면 QR로 보지 않는다.
        // PC가 느려 스캔 글자가 늦게 들어오면 늘린다. 0 이하면 기본값 0.5초를 쓴다
        public float scanCharGapSeconds = 0.5f;

        // 하단 안내 문구 — QR 대기, 시작하기(로컬 모드·이름 없음), QR로 확인한 체험자의 시작하기({name}은 체험자 이름)
        public string qrGuideText = Constants.TitleMessages.QrGuide;
        public string startGuideText = Constants.TitleMessages.StartGuide;
        public string startGuideWithNameText = Constants.TitleMessages.StartGuideWithName;

        // 서버 모드 QR 확인 안내 — 확인 중, 이미 체험 완료, 등록되지 않은 QR, 서버 오류(잠시 보여 준 뒤 QR 대기로 돌아간다)
        public string qrCheckingText = Constants.TitleMessages.QrChecking;
        public string qrCompletedText = Constants.TitleMessages.QrCompleted;
        public string qrNotFoundText = Constants.TitleMessages.QrNotFound;
        public string qrCheckFailedText = Constants.TitleMessages.QrCheckFailed;
    }
}
