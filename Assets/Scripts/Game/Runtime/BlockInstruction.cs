using System.Collections.Generic;

namespace DG.Game.Runtime
{
    public abstract class BlockInstruction
    {
        public CodingBlock Source;
    }

    public sealed class CommandInstruction : BlockInstruction
    {
        public string Command;
        public string Value; // null = Value 블록 없음
    }

    public sealed class ActionInstruction : BlockInstruction
    {
        public string Action;
    }

    public sealed class ConditionActionInstruction : BlockInstruction
    {
        public string Action;
    }

    public sealed class IfInstruction : BlockInstruction
    {
        public string Condition;
        public List<BlockInstruction> Then;
        public List<BlockInstruction> Else;
    }

    public sealed class RepeatInstruction : BlockInstruction
    {
        public int Count;
        public List<BlockInstruction> Body;
    }
}
