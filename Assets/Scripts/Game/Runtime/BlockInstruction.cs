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
