using System;

namespace Data
{
    // StreamingAssets/Json/3_Game.json 매핑 — 게임(코딩) 씬의 연출 타이밍과 조작 감도.
    // 빌드 없이 현장에서 조정하기 위해 분리한 값들이며, 블록 크기·소켓 오프셋처럼 아트와
    // 맞물린 구조 값이나 하이라이트 UV 범위(머티리얼 에셋에서 조정)는 여기에 두지 않는다.
    [Serializable]
    public class GameSceneSettings
    {
        // 완성하기 후 명령을 하나씩 실행하는 것처럼 보이게 두는 간격(ms)
        public int executeStepDelayMs = 200;

        // 컴파일 성공 파도타기 — 시작하기~완성하기 순서로 블록마다 이 간격(ms)만큼 지연 후 초록 페이드인
        public int successWaveStepMs = 120;
        public float successWaveFadeInDuration = 0.18f;

        // 컴파일 실패 깜빡임 — 완전 채도 빨간색으로 N회 깜빡인 뒤 기본 에러 하이라이트로 정착
        public int errorBlinkCount = 2;
        public float errorBlinkHalfDuration = 0.22f;

        // 블록 스냅 판정 반경(px) — 터치 스크린 감도에 맞춰 조정
        public float snapRadius = 120f;
        public float chainSnapRadius = 120f;

        // 블록이 스냅 위치로 붙는 데 걸리는 시간(초)
        public float blockSnapDuration = 0.15f;
    }
}
