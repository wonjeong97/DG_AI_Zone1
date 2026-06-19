namespace DG.Game
{
    public enum BlockCategory
    {
        Control,          // 시작하기, 종료하기 — 진회색, 고정
        Command,          // 태양광 패널의 각도 — 살몬, 우측 ValueSlot
        Value,            // 30도, 동쪽 — 파랑, ValueSlot에 끼워짐
        FlowControl,      // 만약, 반복하기 — 주황, C자 컨테이너
        ConditionAction,  // 전기 과부하, 개방하기 — 핑크
        Action,           // 방향 감지기 작동하기 — 청록
        Logic             // 그리고 — 초록, 수평 체이닝
    }
}
