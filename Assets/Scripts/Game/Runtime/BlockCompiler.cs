using System.Collections.Generic;
using UnityEngine;

namespace DG.Game.Runtime
{
    public readonly struct CompileResult
    {
        public readonly bool Success;
        public readonly List<BlockInstruction> Instructions;
        public readonly string Error;
        public readonly CodingBlock[] ErrorBlocks;

        private CompileResult(bool success, List<BlockInstruction> instr, string err, CodingBlock[] errorBlocks)
        { Success = success; Instructions = instr; Error = err; ErrorBlocks = errorBlocks; }

        public static CompileResult Ok(List<BlockInstruction> instr)           => new(true,  instr, null, null);
        public static CompileResult Fail(string err, CodingBlock block = null) => new(false, null,  err,  block ? new[] { block } : null);
    }

    public static class BlockCompiler
    {
        public static CompileResult Compile(CodingZone zone)
        {
            // '시작하기' 블록은 소켓 계층 어디에나 있을 수 있으므로 전체 탐색
            CodingBlock start = null;
            foreach (var b in zone.GetComponentsInChildren<CodingBlock>())
            {
                if (b.Category == BlockCategory.Control && b.name == "시작하기")
                { start = b; break; }
            }
            if (!start)
                return CompileResult.Fail("'시작하기' 블록이 코딩 영역에 없습니다");

            // transform.Find: 직접 자식만 탐색 — 하위 체인 소켓과 혼동 방지
            ChainOutSocket socket = null;
            start.transform.Find("ChainOutSocket")?.TryGetComponent(out socket);
            if (!socket || !socket.Occupant)
                return CompileResult.Fail("'시작하기'에 연결된 블록이 없습니다", start);

            var program  = new List<BlockInstruction>();
            var terminal = WalkChain(socket.Occupant, program);

            var cmdError = FindCommandWithoutValue(program);
            if (cmdError)
                return CompileResult.Fail($"'{cmdError.name}' 블록에 값 블록이 없습니다", cmdError);

            var condError = FindIfWithoutCondition(program);
            if (condError)
                return CompileResult.Fail($"'{condError.name}' 블록에 조건이 없습니다", condError);

            if (!terminal || terminal.name != "종료하기")
            {
                // 씬 전체(인벤토리 포함)에서 종료하기 블록을 찾아 표시
                CodingBlock endBlock = null;
                foreach (var b in Object.FindObjectsOfType<CodingBlock>())
                    if (b.name == "종료하기") { endBlock = b; break; }
                return CompileResult.Fail("마지막 블록이 '종료하기'여야 합니다", endBlock);
            }

            return CompileResult.Ok(program);
        }

        // ChainOutSocket.Occupant 포인터를 따라 체인을 순회.
        // Control 블록(종료하기)에 도달하면 그 블록을 반환, 체인이 끊기면 null 반환.
        private static CodingBlock WalkChain(CodingBlock current, List<BlockInstruction> output)
        {
            while (current)
            {
                if (current.Category == BlockCategory.Control) return current;

                var instr = Build(current);
                if (instr != null) output.Add(instr);

                ChainOutSocket socket = null;
                current.transform.Find("ChainOutSocket")?.TryGetComponent(out socket);
                current = socket?.Occupant;
            }
            return null;
        }

        private static BlockInstruction Build(CodingBlock block)
        {
            return block.Category switch
            {
                BlockCategory.Command         => BuildCommand(block),
                BlockCategory.Action          => new ActionInstruction         { Source = block, Action  = block.name },
                BlockCategory.ConditionAction => new ConditionActionInstruction{ Source = block, Action  = block.name },
                BlockCategory.FlowControl     => BuildFlowControl(block),
                _                             => null
            };
        }

        private static CommandInstruction BuildCommand(CodingBlock block)
        {
            // ValueOutSocket은 CodingZone.OnDrop이 직접 자식으로 붙임
            ValueOutSocket vos = null;
            block.transform.Find("ValueOutSocket")?.TryGetComponent(out vos);
            return new CommandInstruction
            {
                Source  = block,
                Command = block.name,
                Value   = vos?.Occupant?.name
            };
        }

        private static BlockInstruction BuildFlowControl(CodingBlock block)
        {
            // FlowControl 블록 안에는 "Inner"로 시작하는 자식 컨테이너가 있음
            var inners = new List<Transform>();
            foreach (Transform child in block.transform)
                if (child.name.StartsWith("Inner")) inners.Add(child);

            bool isRepeat = block.name.Contains("반복");

            if (isRepeat)
            {
                var body = new List<BlockInstruction>();
                if (inners.Count > 0) WalkInner(inners[0], body);
                return new RepeatInstruction
                {
                    Source = block,
                    Count  = ReadRepeatCount(block),
                    Body   = body
                };
            }
            else // 만약 (if)
            {
                var then = new List<BlockInstruction>();
                var els  = new List<BlockInstruction>();
                if (inners.Count > 0) WalkInner(inners[0], then);
                if (inners.Count > 1) WalkInner(inners[1], els);
                return new IfInstruction
                {
                    Source    = block,
                    Condition = BuildConditionExpr(block),
                    Then      = then,
                    Else      = els
                };
            }
        }

        // Inner 컨테이너 내부 탐색
        // - 첫 블록에 ChainOutSocket이 있으면 → 사용자가 체인으로 연결한 경우 (소켓 계층 탐색)
        // - 없으면 → 초기 배치된 블록 (직접 자식 순서대로 순회)
        private static void WalkInner(Transform inner, List<BlockInstruction> output)
        {
            CodingBlock first = null;
            foreach (Transform child in inner)
            {
                if (child.TryGetComponent<CodingBlock>(out var b) && b.Category != BlockCategory.Control)
                { first = b; break; }
            }
            if (!first) return;

            if (first.transform.Find("ChainOutSocket"))
            {
                _ = WalkChain(first, output);
            }
            else
            {
                foreach (Transform child in inner)
                {
                    if (!child.TryGetComponent<CodingBlock>(out var b)) continue;
                    if (b.Category == BlockCategory.Control) continue;
                    var instr = Build(b);
                    if (instr != null) output.Add(instr);
                }
            }
        }

        // Command 블록 중 Value가 연결되지 않은 첫 번째 블록을 재귀적으로 탐색
        private static CodingBlock FindCommandWithoutValue(List<BlockInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr is CommandInstruction cmd && cmd.Value == null)
                    return cmd.Source;
                if (instr is RepeatInstruction rep && rep.Body != null)
                {
                    var err = FindCommandWithoutValue(rep.Body);
                    if (err) return err;
                }
                if (instr is IfInstruction ifInstr)
                {
                    if (ifInstr.Then != null) { var err = FindCommandWithoutValue(ifInstr.Then); if (err) return err; }
                    if (ifInstr.Else != null) { var err = FindCommandWithoutValue(ifInstr.Else); if (err) return err; }
                }
            }
            return null;
        }

        // FlowControl(만약) 블록의 헤더 조건 슬롯에서 ConditionExpr를 읽음
        // 체인: [조건1] -ConditionOut→ConditionIn- [그리고/또는] -ConditionOut→ConditionIn- [조건2]
        private static ConditionExpr BuildConditionExpr(CodingBlock flowBlock)
        {
            ValueOutSocket vos = null;
            foreach (Transform child in flowBlock.transform)
            {
                if (!child.name.StartsWith("Header_")) continue;
                child.Find("ValueOutSocket")?.TryGetComponent(out vos);
                if (vos) break;
            }

            if (!vos || !vos.Occupant) return null;

            CodingBlock first = vos.Occupant;

            // 조건1의 ConditionOutSocket에 Logic 블록이 연결됐는지 확인
            ConditionOutSocket firstCondOut = null;
            first.transform.Find("ConditionOutSocket")?.TryGetComponent(out firstCondOut);

            if (firstCondOut && firstCondOut.Occupant &&
                firstCondOut.Occupant.Category == BlockCategory.Logic)
            {
                CodingBlock logic = firstCondOut.Occupant;
                ConditionOutSocket logicCondOut = null;
                logic.transform.Find("ConditionOutSocket")?.TryGetComponent(out logicCondOut);

                CodingBlock right = logicCondOut?.Occupant;
                return new LogicConditionExpr
                {
                    Source   = logic,
                    Operator = logic.name,
                    Left     = new SimpleConditionExpr { Source = first,  Name = first.name },
                    Right    = right ? new SimpleConditionExpr { Source = right, Name = right.name } : null
                };
            }

            // 단순 조건
            if (first.Category == BlockCategory.Condition)
                return new SimpleConditionExpr { Source = first, Name = first.name };

            return null;
        }

        // 만약 블록 중 조건이 연결되지 않은 첫 번째 블록을 재귀 탐색
        private static CodingBlock FindIfWithoutCondition(List<BlockInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr is IfInstruction ifInstr)
                {
                    if (ifInstr.Condition == null) return ifInstr.Source;
                    if (ifInstr.Then != null) { var err = FindIfWithoutCondition(ifInstr.Then); if (err) return err; }
                    if (ifInstr.Else != null) { var err = FindIfWithoutCondition(ifInstr.Else); if (err) return err; }
                }
                else if (instr is RepeatInstruction rep && rep.Body != null)
                {
                    var err = FindIfWithoutCondition(rep.Body);
                    if (err) return err;
                }
            }
            return null;
        }

        // 반복하기 블록의 헤더에 달린 Value 블록에서 횟수를 읽음
        private static int ReadRepeatCount(CodingBlock block)
        {
            foreach (Transform child in block.transform)
            {
                if (!child.name.StartsWith("Header_")) continue;
                ValueOutSocket vos = null;
                child.Find("ValueOutSocket")?.TryGetComponent(out vos);
                if (vos && vos.Occupant && int.TryParse(vos.Occupant.name, out var n))
                    return n;
            }
            return 1;
        }
    }
}
