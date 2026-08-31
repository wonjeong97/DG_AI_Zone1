using System.Collections.Generic;

namespace Game.Runtime
{
    public abstract class BlockInstruction
    {
        public CodingBlock Source;
    }

    public sealed class CommandInstruction : BlockInstruction
    {
        public string Command;
        public string Value; // null = Value 블록 없음
        public ValueKind ValueKind; // 연결된 Value 블록의 타입
        public CodingBlock ValueSource; // 연결된 Value 블록 자신 — 컴파일 성공 시 초록 외곽선 표시용
    }

    public sealed class ActionInstruction : BlockInstruction
    {
        public string Action;
    }

    public sealed class ConditionActionInstruction : BlockInstruction
    {
        public string Action;
    }

    // ── 조건식 계층 ─────────────────────────────────────────────────
    public abstract class ConditionExpr { }

    public sealed class SimpleConditionExpr : ConditionExpr
    {
        public CodingBlock Source;
        public string Name;
    }

    public sealed class LogicConditionExpr : ConditionExpr
    {
        public CodingBlock Source;
        public string Operator; // "그리고" or "또는"
        public SimpleConditionExpr Left;
        public SimpleConditionExpr Right;
    }

    public sealed class IfInstruction : BlockInstruction
    {
        public ConditionExpr Condition;
        public List<BlockInstruction> Then;
        public List<BlockInstruction> Else;

        // '아니면' 블록이 실제로 배치됐는지 — Else가 비어 있어도(뒤에 아무 블록도 없어도) true일 수 있다.
        // 표시용 판단(포매터가 빈 아니면 {}를 출력할지)에만 쓰인다.
        public bool HasElseMarker;

        // HasElseMarker가 true일 때 그 '아니면' 블록 자신 — 컴파일 성공 시 초록 외곽선 표시용
        public CodingBlock ElseMarkerSource;
    }

    // 만약 블록의 Inner 체인 밖(메인 체인/반복문 등)에 잘못 놓인 '아니면' 블록의 표시용 노드.
    // 컴파일 실패(FindElseOutsideIf) 시 디버그 코드 표시에만 쓰이고, 정상 실행 경로에는 등장하지 않는다.
    public sealed class ElseInstruction : BlockInstruction
    {
        public List<BlockInstruction> Body = new();
    }

    public sealed class RepeatInstruction : BlockInstruction
    {
        public int Count; // 음수 = 무한 반복 (횟수 Value 미연결)
        public List<BlockInstruction> Body;

        public bool IsInfinite => Count < 0;
    }

    public sealed class FunctionInstruction : BlockInstruction
    {
        public string Name;    // 함수 호출 블록 이름 (예: "함수")
        public string DefName; // 함수 정의 블록 이름 (예: "함수 정의")
        public List<BlockInstruction> Body; // 함수 정의 내부에 들어있는 명령어 목록
    }
}
