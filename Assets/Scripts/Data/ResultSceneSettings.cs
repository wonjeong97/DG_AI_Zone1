using System;

namespace Data
{
    // StreamingAssets/Json/4_Result.json 매핑 — 결과 씬 연출 타이밍.
    // 전체 시퀀스(플레이어 블록 조립 → 패널 회전 → 효율 카운트업 → AI 안내 → AI 반복)의 길이를
    // 좌우하는 값들만 모았다. 판정 경계(부족/보통/양호)나 결과 텍스트 포맷은 채점·콘텐츠 규칙이라
    // 여기에 두지 않는다.
    [Serializable]
    public class ResultSceneSettings
    {
        // 결과 패널에 다시 조립한 블록의 배율 — 게임 코딩 패널 블록 크기 대비(칸보다 넓은 묶음은 폭에 맞춰 더 줄인다)
        public float programBlockScale = 0.6f;

        // 결과 패널 블록이 아래에서 올라와 맞물리는 시간(초) — 아래로 잇는 블록·ㄷ자 안쪽 블록
        public float programBlockRiseDuration = 0.7f;

        // 값·조건 블록이 오른쪽에서 미끄러져 와 붙는 시간(초)
        public float programValueSlideDuration = 0.4f;

        // 블록 하나가 붙은 뒤 다음 블록까지 쉬는 시간(초)
        public float programBlockInterval = 0.3f;

        // 블록이 칸 아래로 벗어나 자동 스크롤하는 시간(초)
        public float programScrollDuration = 0.3f;

        // 에너지 효율 0% -> N% 카운트업 시간(초)
        public float effCountDuration = 1.6f;

        // AI 결과가 모두 나온 뒤 '화면을 터치하면 다음으로 넘어갑니다' 안내를 띄우기까지 기다리는 시간(초)
        public float touchGuideDelay = 1f;

        // 3D 태양광 패널 자세 애니메이션 시간(초) — 방향/각도가 순차 재생되므로 실제 소요는 이 값의 2배
        public float panelPoseDuration = 1.5f;

        // 풍차 회전 속도 램프업 시간(초) — 레벨2 결과 스테이지. 정지 상태에서 효율에 비례한 속도까지
        public float turbineSpinDuration = 1.5f;

        // 댐 수문 개방 애니메이션 시간(초) — 레벨3 결과 스테이지. 닫힌 상태에서 효율에 비례한 개방량까지.
        // 수문이 열리고 물이 마루에서 토우까지 내려가는 시간을 합친 값이라 다른 스테이지보다 길다.
        public float damOpenDuration = 3f;

        // 발전소 피스톤·수증기 강도 램프업 시간(초) — 레벨4 결과 스테이지
        public float plantPumpDuration = 1.5f;

        // 연구소 건물 안 조명이 밝아지는 시간(초) — 레벨5 결과 스테이지. 꺼진 상태에서 효율에 비례한 밝기까지
        public float labLightDuration = 1.5f;

        // 'AI가 코딩을 시작합니다' 안내를 띄워 두는 시간(초)
        public float aiStartHold = 3f;

        // 그 안내 뒤 말줄임(...) 점 애니메이션 간격(ms)
        public int aiCodingDotIntervalMs = Constants.ResultMessages.AiCodingDotIntervalMs;

        // 상단 안내 문구 뒤 말줄임( · · · ) 점 애니메이션 간격(ms)
        public int topDotIntervalMs = Constants.ResultMessages.ResultTopDotIntervalMs;

        /// <summary>
        /// 음수 시간·간격을 0으로 바꾸고, 바꾼 값이 있으면 true를 돌려준다.
        /// </summary>
        public bool ClampToValid()
        {
            bool changed = false;
            programBlockScale = SettingsClamp.NonNegative(programBlockScale, ref changed);
            programBlockRiseDuration = SettingsClamp.NonNegative(programBlockRiseDuration, ref changed);
            programValueSlideDuration = SettingsClamp.NonNegative(programValueSlideDuration, ref changed);
            programBlockInterval = SettingsClamp.NonNegative(programBlockInterval, ref changed);
            programScrollDuration = SettingsClamp.NonNegative(programScrollDuration, ref changed);
            effCountDuration = SettingsClamp.NonNegative(effCountDuration, ref changed);
            touchGuideDelay = SettingsClamp.NonNegative(touchGuideDelay, ref changed);
            panelPoseDuration = SettingsClamp.NonNegative(panelPoseDuration, ref changed);
            turbineSpinDuration = SettingsClamp.NonNegative(turbineSpinDuration, ref changed);
            damOpenDuration = SettingsClamp.NonNegative(damOpenDuration, ref changed);
            plantPumpDuration = SettingsClamp.NonNegative(plantPumpDuration, ref changed);
            labLightDuration = SettingsClamp.NonNegative(labLightDuration, ref changed);
            aiStartHold = SettingsClamp.NonNegative(aiStartHold, ref changed);
            aiCodingDotIntervalMs = SettingsClamp.NonNegative(aiCodingDotIntervalMs, ref changed);
            topDotIntervalMs = SettingsClamp.NonNegative(topDotIntervalMs, ref changed);
            return changed;
        }
    }
}
