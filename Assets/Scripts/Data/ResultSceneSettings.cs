using System;

namespace Data
{
    // StreamingAssets/Json/4_Result.json 매핑 — 결과 씬 연출 타이밍.
    // 전체 시퀀스(플레이어 타이핑 → 패널 회전 → 효율 카운트업 → AI 안내 → AI 반복)의 길이를
    // 좌우하는 값들만 모았다. 판정 경계(부족/보통/양호)나 결과 텍스트 포맷은 채점·콘텐츠 규칙이라
    // 여기에 두지 않는다.
    [Serializable]
    public class ResultSceneSettings
    {
        // 결과 텍스트가 한 글자씩 찍히는 간격(초)
        public float typewriterCharInterval = 0.03f;

        // 에너지 효율 0% -> N% 카운트업 시간(초)
        public float effCountDuration = 0.8f;

        // 3D 태양광 패널 자세 애니메이션 시간(초) — 방향/각도가 순차 재생되므로 실제 소요는 이 값의 2배
        public float panelPoseDuration = 1.5f;

        // 풍차 회전 속도 램프업 시간(초) — 레벨2 결과 스테이지. 정지 상태에서 효율에 비례한 속도까지
        public float turbineSpinDuration = 1.5f;

        // 댐 수문 개방 애니메이션 시간(초) — 레벨3 결과 스테이지. 닫힌 상태에서 효율에 비례한 개방량까지.
        // 수문이 열리고 물이 마루에서 토우까지 내려가는 시간을 합친 값이라 다른 스테이지보다 길다.
        public float damOpenDuration = 3f;

        // 발전소 피스톤·수증기 강도 램프업 시간(초) — 레벨4 결과 스테이지
        public float plantPumpDuration = 1.5f;

        // 'AI가 코딩을 시작합니다' 안내를 띄워 두는 시간(초)
        public float aiStartHold = 3f;

        // 그 안내 뒤 말줄임(...) 점 애니메이션 간격(ms)
        public int aiCodingDotIntervalMs = 400;

        // 상단 안내 문구 뒤 말줄임( · · · ) 점 애니메이션 간격(ms)
        public int topDotIntervalMs = 400;
    }
}
