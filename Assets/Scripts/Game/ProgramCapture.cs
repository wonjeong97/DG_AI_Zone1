using System.Collections.Generic;
using Data;
using Game.Runtime;

namespace Game
{
    // 코딩 패널에 조립한 블록 모양을 읽어 결과 씬으로 넘길 ProgramShape로 만든다.
    public static class ProgramCapture
    {
        /// <summary>
        /// 코딩 패널의 시작하기 블록에서 이어진 블록과 함수 정의 블록을 소켓 연결 그대로 읽는다(패널 밖·인벤토리 블록은 뺀다).
        /// </summary>
        public static ProgramShape Capture(CodingZone zone)
        {
            ProgramShape shape = new ProgramShape();
            if (!zone) return shape;

            foreach (CodingBlock block in zone.Blocks)
            {
                if (!block || !zone.Contains(block)) continue;

                if (block.Category == BlockCategory.Control && block.ControlRole == ControlRole.Start)
                {
                    if (shape.Start is null) shape.Start = CaptureBlock(block);
                }
                else if (block.Category == BlockCategory.FunctionDef)
                {
                    shape.FunctionDefs.Add(CaptureBlock(block));
                }
            }
            return shape;
        }

        /// <summary>
        /// 블록 하나와 그 소켓(값·조건·안쪽·아래)에 붙은 블록을 재귀로 읽는다.
        /// </summary>
        private static ProgramBlock CaptureBlock(CodingBlock block)
        {
            // 블록 이름은 BlockFactory가 레이아웃 라벨로 맞춰 둔다(컴파일·채점도 이름으로 읽는다)
            ProgramBlock node = new ProgramBlock
            {
                Category = block.Category,
                Label = block.name,
                ValueKind = block.ValueKind,
                ControlRole = block.ControlRole
            };

            node.Value = CaptureOccupant(block.GetSocket<ValueOutSocket>());
            node.Condition = CaptureOccupant(block.GetSocket<ConditionOutSocket>());

            // 재귀 호출마다 소켓 목록이 필요해 공용 버퍼 대신 새 목록을 쓴다(완성하기 때 한 번만 불린다)
            List<InnerSocket> inners = new List<InnerSocket>();
            block.GetSockets(inners);
            foreach (InnerSocket inner in inners)
                node.Inners.Add(CaptureOccupant(inner));

            node.Next = CaptureOccupant(block.GetSocket<ChainOutSocket>());
            return node;
        }

        /// <summary>
        /// 소켓에 붙은 블록을 읽는다(소켓이 없거나 비어 있으면 null).
        /// </summary>
        private static ProgramBlock CaptureOccupant(BlockSocket socket)
        {
            return socket && socket.Occupant ? CaptureBlock(socket.Occupant) : null;
        }
    }
}
