using System.Collections.Generic;
using Data;

namespace Game.Runtime
{
    // 코딩 패널에 조립한 블록 하나와 그 소켓에 붙은 블록들 — 결과 씬이 같은 블록으로 다시 조립한다.
    public sealed class ProgramBlock
    {
        public BlockCategory Category;
        public string Label;
        public ValueKind ValueKind;
        public ControlRole ControlRole;

        // 값 슬롯(명령의 값, 만약의 조건)에 붙은 블록
        public ProgramBlock Value;

        // 조건 체인(조건 → 그리고/또는 → 조건)에서 오른쪽에 이어 붙은 블록
        public ProgramBlock Condition;

        // 반복하기·만약·함수 정의 안쪽 소켓마다 그 안의 첫 블록(비어 있으면 null) — 블록의 소켓 순서와 같다
        public readonly List<ProgramBlock> Inners = new();

        // 아래로 이어 붙은 다음 블록
        public ProgramBlock Next;

        /// <summary>
        /// 블록을 만들 때 쓰는 레이아웃 항목으로 바꾼다.
        /// </summary>
        public BlockEntry ToEntry() => new BlockEntry
        {
            category = Category,
            label = Label,
            valueKind = ValueKind,
            controlRole = ControlRole
        };

        /// <summary>
        /// 레이아웃 항목의 블록 정보만 옮겨 새 블록을 만든다(소켓에 붙은 블록은 비어 있다).
        /// </summary>
        public static ProgramBlock From(BlockEntry entry) => new ProgramBlock
        {
            Category = entry.category,
            Label = entry.label,
            ValueKind = entry.valueKind,
            ControlRole = entry.controlRole
        };
    }

    // 한 판의 블록 조립 모양 — 시작하기에서 이어진 체인과, 체인과 따로 놓이는 함수 정의 블록들.
    public sealed class ProgramShape
    {
        public ProgramBlock Start;
        public readonly List<ProgramBlock> FunctionDefs = new();
    }
}
