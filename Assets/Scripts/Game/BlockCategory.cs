namespace Game
{
    public enum BlockCategory
    {
        Control,          // 시작하기, 완성하기 — 진회색, 고정
        Command,          // 태양광 패널의 각도 — 살몬, 우측 ValueSlot
        Value,            // 30도, 동쪽 — 파랑, ValueSlot에 끼워짐
        FlowControl,      // 만약, 반복하기 — 주황, C자 컨테이너
        ConditionAction,  // 전기 과부하, 개방하기 — 핑크
        Action,           // 방향 감지기 작동하기 — 청록
        Logic,            // 그리고 — 초록, 수평 체이닝
        Condition,
        Function,         // 함수(사용) — Func 아트, CommandNoValue와 동일한 크기·체인 소켓
        FunctionDef,      // 함수(구현) — FuncBody 아트, FlowControl과 동일한 C자 컨테이너(값 슬롯 없음)
        Else              // 아니면 — Else 아트, CommandNoValue와 동일한 크기·체인 소켓. 만약 블록의 Inner 체인 안에 놓여 Then/Else 분기 경계로 쓰인다
    }
}
