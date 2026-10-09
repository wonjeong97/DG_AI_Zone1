using System.Collections.Generic;
using Cysharp.Text;
using UnityEngine;

namespace Game.Runtime
{
    // 호출부가 에러 메시지 문자열을 비교하지 않고 분기할 수 있게 한다.
    /// <summary>
    /// 컴파일 실패 사유를 분류한다.
    /// </summary>
    public enum CompileErrorKind
    {
        None,
        Generic,
        UnusedBlocks,
    }

    public readonly struct CompileResult
    {
        public readonly bool Success;
        public readonly List<BlockInstruction> Instructions;
        public readonly string Error;
        public readonly CompileErrorKind ErrorKind;
        public readonly CodingBlock[] ErrorBlocks;

        // 검증 성공/실패와 무관하게 시작하기 이후 순회된 프로그램 (로그·코드 표시용, 순회 전 실패면 null)
        public readonly List<BlockInstruction> Program;

        // 체인이 완성하기(End) 블록까지 도달했는지 — 코드 표시 시 END 출력 여부 판단용
        public readonly bool ReachedEnd;

        /// <summary>
        /// 컴파일 결과 값을 채운다.
        /// </summary>
        private CompileResult(bool success, List<BlockInstruction> instr, string err, CompileErrorKind errorKind,
            CodingBlock[] errorBlocks, List<BlockInstruction> program, bool reachedEnd)
        {
            Success = success; Instructions = instr; Error = err; ErrorKind = errorKind;
            ErrorBlocks = errorBlocks; Program = program; ReachedEnd = reachedEnd;
        }

        /// <summary>
        /// 성공 결과를 만든다 (성공은 항상 완성하기에 도달한 상태).
        /// </summary>
        public static CompileResult Ok(List<BlockInstruction> instr)
            => new(true, instr, null, CompileErrorKind.None, null, instr, true);

        /// <summary>
        /// 문제 블록 하나를 표시하는 실패 결과를 만든다.
        /// </summary>
        public static CompileResult Fail(string err, CodingBlock block = null,
            List<BlockInstruction> program = null, bool reachedEnd = false,
            CompileErrorKind kind = CompileErrorKind.Generic)
            => new(false, null, err, kind, block ? new[] { block } : null, program, reachedEnd);

        /// <summary>
        /// 문제 블록 여러 개를 표시하는 실패 결과를 만든다.
        /// </summary>
        public static CompileResult Fail(string err, CodingBlock[] blocks,
            List<BlockInstruction> program = null, bool reachedEnd = false,
            CompileErrorKind kind = CompileErrorKind.Generic)
            => new(false, null, err, kind, blocks, program, reachedEnd);
    }

    public static class BlockCompiler
    {
        // Compile() 1회 동안만 쓰는 순회 상태 — 정적 필드에 두면 씬을 떠난 뒤에도 파괴된 블록을 붙잡고 있게 된다
        private sealed class CompileContext
        {
            public readonly CodingZone Zone;

            // 이번 컴파일에서 검사할 블록 전체(코딩 패널 + 인벤토리, 비활성 탭 포함) — CodingZone이 관리하는 목록
            public readonly IReadOnlyList<CodingBlock> AllBlocks;

            // WalkInnerWithElse가 '만약' 블록 안에서 실제로 만난 '아니면' 블록
            public readonly HashSet<CodingBlock> VisitedElseBlocks = new();

            // 한 '만약' 안에서 두 번째 이후로 만난 '아니면' 블록 — 분기를 다시 나눌 수 없어 에러로 알린다
            public readonly List<CodingBlock> DuplicateElseBlocks = new();

            // 함수 정의를 펼치는 중인지 — 정의 안에서 만난 함수 호출을 펼치면 끝없이 재귀하므로 막는다
            public bool IsExpandingFunction;
            public CodingBlock FunctionCallInsideDef;

            /// <summary>
            /// 검사할 코딩 존과 블록 목록을 정한다.
            /// </summary>
            public CompileContext(CodingZone zone)
            {
                Zone = zone;
                AllBlocks = zone ? zone.Blocks : System.Array.Empty<CodingBlock>();
            }
        }

        /// <summary>
        /// 코딩 패널의 블록 연결을 시작하기부터 순회해 명령 목록으로 만들고, 배치 규칙을 검증한다.
        /// </summary>
        public static CompileResult Compile(CodingZone zone)
        {
            CompileContext ctx = new CompileContext(zone);

            CodingBlock start = FindStartBlock(ctx);
            if (!start)
                return CompileResult.Fail(Constants.CompilerMessages.MissingStartBlock);

            // 시작하기가 다른 블록의 체인/내부 소켓에 연결돼 있으면 첫 블록이 아님
            Transform startParent = start.transform.parent;
            if (startParent &&
                (startParent.TryGetComponent<ChainOutSocket>(out _) || startParent.TryGetComponent<InnerSocket>(out _)))
                return CompileResult.Fail(Constants.CompilerMessages.StartNotFirst, start);

            // 블록 직속 소켓만 사용 — 하위 체인 소켓과 혼동 방지
            ChainOutSocket socket = ChainOutSocket.OfBlock(start);
            if (!socket || !socket.Occupant)
                return CompileResult.Fail(Constants.CompilerMessages.MissingConnectedAfterStart, start);

            List<BlockInstruction> program = new List<BlockInstruction>();
            CodingBlock terminal = WalkChain(ctx, socket.Occupant, program);
            bool reachedEnd = terminal && terminal.ControlRole == Data.ControlRole.End;

            return Validate(ctx, start, terminal, program, reachedEnd);
        }

        /// <summary>
        /// '시작하기' 블록을 찾는다 — 소켓 계층 어디에나 있을 수 있으므로 패널 위 블록 전체에서 찾는다.
        /// </summary>
        private static CodingBlock FindStartBlock(CompileContext ctx)
        {
            foreach (CodingBlock b in ctx.AllBlocks)
            {
                if (!IsActiveInZone(ctx.Zone, b)) continue;
                if (b.Category == BlockCategory.Control && b.ControlRole == Data.ControlRole.Start)
                    return b;
            }
            return null;
        }

        /// <summary>
        /// 순회한 프로그램이 배치 규칙을 지키는지 차례로 검사해 첫 번째 위반을 실패로 돌려준다.
        /// </summary>
        private static CompileResult Validate(CompileContext ctx, CodingBlock start, CodingBlock terminal,
            List<BlockInstruction> program, bool reachedEnd)
        {
            // 움직이기 블록은 쓰지 않아도 되지만, 시작하기와 완성하기를 바로 잇는 빈 프로그램은 막는다
            if (reachedEnd && program.Count == 0)
                return CompileResult.Fail(Constants.CompilerMessages.EmptyBetweenStartEnd,
                    new[] { start, terminal }, program, reachedEnd);

            // 함수 호출 블록을 연결했으면 함수 정의 블록도 코딩 영역에 놓여 있어야 한다 (정의 안이 비어 있는 것은 허용).
            // 정의 블록은 인벤토리에 있을 수 있어 미사용 블록처럼 탭을 전환해 보여 준다
            CodingBlock functionCall = FindFunctionCall(program);
            if (functionCall)
            {
                CodingBlock funcDef = FindSceneBlock(ctx, BlockCategory.FunctionDef);
                if (!ctx.Zone || !ctx.Zone.Contains(funcDef))
                    return CompileResult.Fail(Constants.CompilerMessages.FunctionDefNotPlaced,
                        funcDef ? new[] { funcDef, functionCall } : new[] { functionCall },
                        program, reachedEnd, CompileErrorKind.UnusedBlocks);
            }

            CodingBlock callInsideDef = FindFunctionCallInsideDef(ctx);
            if (callInsideDef)
                return CompileResult.Fail(Constants.CompilerMessages.FunctionCallInsideDef,
                    callInsideDef, program, reachedEnd);

            CodingBlock cmdError = FindCommandWithoutValue(program);
            if (cmdError)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.CommandWithoutValueFormat, cmdError.name),
                    cmdError, program, reachedEnd);

            CodingBlock condError = FindIfWithoutCondition(program);
            if (condError)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.IfWithoutConditionFormat, condError.name),
                    condError, program, reachedEnd);

            CodingBlock logicError = FindLogicWithoutRight(program);
            if (logicError)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.LogicMissingRightFormat, logicError.name),
                    logicError, program, reachedEnd);

            // 조건을 세 개 이상 이으면 앞의 두 조건만 채점되므로 뒤에 이은 블록을 표시하고 막는다
            CodingBlock[] conditionOverflow = FindConditionOverflow(program);
            if (conditionOverflow is not null)
                return CompileResult.Fail(Constants.CompilerMessages.ConditionChainTooLong,
                    conditionOverflow, program, reachedEnd);

            CodingBlock emptyFlow = FindFlowControlWithEmptyInner(program);
            if (emptyFlow)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.EmptyFlowInnerFormat, emptyFlow.name),
                    emptyFlow, program, reachedEnd);

            if (ctx.DuplicateElseBlocks.Count > 0)
                return CompileResult.Fail(Constants.CompilerMessages.DuplicateElse,
                    ctx.DuplicateElseBlocks.ToArray(), program, reachedEnd);

            // 씬의 모든 실행 블록(FlowControl, Action 등 — 인벤토리·방치 블록 포함)이 프로그램에 포함돼야 함 (움직이기는 제외).
            // '아니면' 위치 검사보다 먼저 한다 — 연결하지 않은 '만약' 안의 '아니면'은 아니면이 아니라 만약이 문제이기 때문이다
            CodingBlock[] unused = FindUnusedExecutableBlocks(ctx, program);
            if (unused.Length > 0)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.UnusedBlocksFormat, unused.Length),
                    unused, program, reachedEnd, CompileErrorKind.UnusedBlocks);

            // 코딩 존에 있는 '아니면' 블록 중 '만약' 블록 안에 놓이지 않은 것 — 빨간 외곽선 표시
            CodingBlock[] misplacedElse = FindElseOutsideIf(ctx);
            if (misplacedElse.Length > 0)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.ElseOutsideIfFormat, misplacedElse.Length),
                    misplacedElse, program, reachedEnd);

            if (!reachedEnd)
            {
                // 씬 전체(인벤토리 포함)에서 완성하기 블록을 찾아 표시
                CodingBlock endBlock = null;
                foreach (CodingBlock b in ctx.AllBlocks)
                    if (b && b.ControlRole == Data.ControlRole.End) { endBlock = b; break; }
                return CompileResult.Fail(Constants.CompilerMessages.MissingEndBlock, endBlock, program, reachedEnd);
            }

            return CompileResult.Ok(program);
        }

        /// <summary>
        /// 블록이 파괴되지 않았고, 활성 상태로 코딩 패널 안에 놓여 있는지 확인한다.
        /// </summary>
        private static bool IsActiveInZone(CodingZone zone, CodingBlock block)
        {
            return block && block.gameObject.activeInHierarchy && zone.Contains(block);
        }

        /// <summary>
        /// ChainOutSocket.Occupant를 따라 체인을 순회해 Control 블록(완성하기)에 도달하면 그 블록을, 체인이 끊기면 null을 반환한다.
        /// </summary>
        private static CodingBlock WalkChain(CompileContext ctx, CodingBlock current, List<BlockInstruction> output)
        {
            while (current)
            {
                if (current.Category == BlockCategory.Control) return current;

                AppendBlock(ctx, current, output);

                ChainOutSocket socket = ChainOutSocket.OfBlock(current);
                current = socket ? socket.Occupant : null;
            }
            return null;
        }

        /// <summary>
        /// 블록 하나를 명령 노드로 만들어 출력 목록에 붙인다 — 함수 호출 블록은 함수 정의 본문을 펼쳐 담는다.
        /// </summary>
        private static void AppendBlock(CompileContext ctx, CodingBlock block, List<BlockInstruction> output)
        {
            if (block.Category != BlockCategory.Function)
            {
                BlockInstruction instr = Build(ctx, block);
                if (instr is not null) output.Add(instr);
                return;
            }

            List<BlockInstruction> body = new List<BlockInstruction>();
            string defName = null;
            if (ctx.IsExpandingFunction)
            {
                // 함수 정의 안에 놓인 함수 호출 — 펼치면 끝없이 재귀하므로 빈 본문으로 두고 에러로 알린다
                if (!ctx.FunctionCallInsideDef) ctx.FunctionCallInsideDef = block;
            }
            else
            {
                defName = ExpandFunctionCall(ctx, body);
            }

            output.Add(new FunctionInstruction
            {
                Source = block,
                Name = block.name,
                DefName = defName ?? Constants.CategoryNames.FunctionDef,
                Body = body
            });
        }

        /// <summary>
        /// 함수 정의(FunctionDef) 블록의 Inner 컨테이너에 배치된 블록들을 순서대로 읽어 프로그램에 펼친다.
        /// (프로토타입 — 함수는 1개 가정: 씬에서 첫 FunctionDef 블록을 사용)
        /// </summary>
        private static string ExpandFunctionCall(CompileContext ctx, List<BlockInstruction> output)
        {
            CodingBlock funcDef = FindSceneBlock(ctx, BlockCategory.FunctionDef);
            if (!funcDef) return null;

            List<InnerSocket> inners = new List<InnerSocket>();
            funcDef.GetSockets(inners);

            ctx.IsExpandingFunction = true;
            try
            {
                foreach (InnerSocket inner in inners)
                    WalkInner(ctx, inner, output);
            }
            finally
            {
                ctx.IsExpandingFunction = false;
            }

            return funcDef.name;
        }

        /// <summary>
        /// 블록 카테고리에 맞는 명령 노드를 만든다 (명령이 아닌 블록은 null).
        /// </summary>
        private static BlockInstruction Build(CompileContext ctx, CodingBlock block)
        {
            return block.Category switch
            {
                BlockCategory.Command         => BuildCommand(block),
                BlockCategory.Action          => new ActionInstruction         { Source = block, Action  = block.name },
                BlockCategory.ConditionAction => new ConditionActionInstruction{ Source = block, Action  = block.name },
                BlockCategory.FlowControl     => BuildFlowControl(ctx, block),
                // '만약' Inner 체인 밖(WalkInnerWithElse를 거치지 않은 경로)에 놓인 '아니면' — 잘못된 배치라
                // 컴파일은 실패하지만(FindElseOutsideIf), 실패 시에도 표시되는 코드에는 제자리에 나타나야 한다
                BlockCategory.Else            => new ElseInstruction{ Source = block },
                _                             => null
            };
        }

        /// <summary>
        /// Command 블록과 값 슬롯에 연결된 Value 블록으로 명령 노드를 만든다.
        /// </summary>
        private static CommandInstruction BuildCommand(CodingBlock block)
        {
            ValueOutSocket vos = block.GetSocket<ValueOutSocket>();
            CodingBlock occupant = vos ? vos.Occupant : null;
            return new CommandInstruction
            {
                Source      = block,
                Command     = block.name,
                Value       = occupant ? occupant.name : null,
                ValueKind   = occupant ? occupant.ValueKind : ValueKind.None,
                ValueSource = occupant
            };
        }

        /// <summary>
        /// 반복하기/만약 블록을 내부 본문(과 조건·else 분기)까지 포함한 명령 노드로 만든다.
        /// </summary>
        private static BlockInstruction BuildFlowControl(CompileContext ctx, CodingBlock block)
        {
            // FlowControl 블록의 Inner 컨테이너 진입 소켓 — 등록 순서가 배치 순서(첫 번째가 본문)
            List<InnerSocket> inners = new List<InnerSocket>();
            block.GetSockets(inners);

            if (block.IsRepeat)
            {
                List<BlockInstruction> body = new List<BlockInstruction>();
                if (inners.Count > 0) WalkInner(ctx, inners[0], body);
                int count = ReadRepeatCount(block, out CodingBlock countValueSource);
                return new RepeatInstruction
                {
                    Source      = block,
                    Count       = count,
                    Body        = body,
                    ValueSource = countValueSource
                };
            }

            // 만약 (if)
            List<BlockInstruction> then = new List<BlockInstruction>();
            List<BlockInstruction> els  = new List<BlockInstruction>();
            CodingBlock elseMarker = inners.Count > 0 ? WalkInnerWithElse(ctx, inners[0], then, els) : null;
            return new IfInstruction
            {
                Source           = block,
                Condition        = BuildConditionExpr(block),
                Then             = then,
                Else             = els,
                HasElseMarker    = elseMarker,
                ElseMarkerSource = elseMarker
            };
        }

        /// <summary>
        /// 만약 블록의 Inner 체인을 탐색해 "아니면" 블록 이후는 els로 출력 대상을 전환하고, 실제로 만난 "아니면" 블록을 반환한다 (없으면 null).
        /// </summary>
        private static CodingBlock WalkInnerWithElse(CompileContext ctx, InnerSocket innerSocket,
            List<BlockInstruction> then, List<BlockInstruction> els)
        {
            // "아니면" 블록 자신은 구분 표시일 뿐이라 어느 분기에도 포함되지 않는다.
            // 반환값은 els가 비어 있어도 마커가 있었는지 구분하는 용도다.
            CodingBlock current = innerSocket ? innerSocket.Occupant : null;
            CodingBlock elseMarker = null;
            List<BlockInstruction> output = then;

            while (current)
            {
                if (current.Category == BlockCategory.Control) break;

                if (current.Category == BlockCategory.Else)
                {
                    // 만약 안에 놓인 것은 맞으므로 위치 검사에서는 빼고, 두 번째부터는 따로 모아 에러로 알린다
                    ctx.VisitedElseBlocks.Add(current);
                    if (elseMarker)
                    {
                        ctx.DuplicateElseBlocks.Add(current);
                    }
                    else
                    {
                        elseMarker = current;
                        output = els;
                    }
                }
                else
                {
                    AppendBlock(ctx, current, output);
                }

                ChainOutSocket socket = ChainOutSocket.OfBlock(current);
                current = socket ? socket.Occupant : null;
            }

            return elseMarker;
        }

        /// <summary>
        /// Inner 컨테이너의 진입 소켓에 연결된 블록부터 체인을 따라 읽는다.
        /// </summary>
        private static void WalkInner(CompileContext ctx, InnerSocket innerSocket, List<BlockInstruction> output)
        {
            if (!innerSocket || !innerSocket.Occupant) return;
            _ = WalkChain(ctx, innerSocket.Occupant, output);
        }

        /// <summary>
        /// Command 블록 중 Value가 연결되지 않은 첫 번째 블록을 찾는다 (중첩 본문 포함).
        /// </summary>
        private static CodingBlock FindCommandWithoutValue(List<BlockInstruction> instructions)
        {
            // ValueKind.None(값 슬롯 없는 동작 블록)은 검사 대상에서 제외한다.
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is CommandInstruction cmd && cmd.Value is null
                    && cmd.Source && cmd.Source.ValueKind != ValueKind.None)
                    return cmd.Source;
            }
            return null;
        }

        /// <summary>
        /// 프로그램에 포함되지 않은 실행 블록(FlowControl, Action 등)을 찾는다 — 인벤토리·코딩존 방치 블록 모두 대상.
        /// </summary>
        private static CodingBlock[] FindUnusedExecutableBlocks(CompileContext ctx, List<BlockInstruction> program)
        {
            HashSet<CodingBlock> used = new HashSet<CodingBlock>();
            foreach (BlockInstruction instr in InstructionTree.Traverse(program))
                if (instr.Source) used.Add(instr.Source);

            List<CodingBlock> unused = new List<CodingBlock>();
            foreach (CodingBlock b in ctx.AllBlocks)
            {
                if (b && IsExecutable(b.Category) && !used.Contains(b))
                    unused.Add(b);
            }
            return unused.ToArray();
        }

        // 움직이기(Command) 블록은 쓰지 않아도 된다 — 빠진 만큼 채점에서 점수를 받지 못할 뿐이다.
        /// <summary>
        /// 실행 흐름에 반드시 편입돼야 하는 카테고리인지 확인한다 (값·조건·제어 블록은 단독으로 남아도 무방).
        /// </summary>
        private static bool IsExecutable(BlockCategory category) =>
            category is BlockCategory.FlowControl
                     or BlockCategory.Action
                     or BlockCategory.ConditionAction;

        /// <summary>
        /// FlowControl(만약) 블록의 헤더 조건 슬롯에서 조건식을 읽는다.
        /// </summary>
        private static ConditionExpr BuildConditionExpr(CodingBlock flowBlock)
        {
            // 체인: [조건1] -ConditionOut→ConditionIn- [그리고/또는] -ConditionOut→ConditionIn- [조건2]
            // 조건2 뒤에 더 이은 블록은 조건식에 넣지 않고 Overflow에 모아 컴파일 에러로 알린다.
            ValueOutSocket vos = flowBlock.GetSocket<ValueOutSocket>();
            if (!vos || !vos.Occupant) return null;

            CodingBlock first = vos.Occupant;

            // 조건1의 ConditionOutSocket에 Logic 블록이 연결됐는지 확인
            CodingBlock logic = NextInConditionChain(first);
            if (logic && logic.Category == BlockCategory.Logic)
            {
                CodingBlock right = NextInConditionChain(logic);
                return new LogicConditionExpr
                {
                    Source   = logic,
                    Operator = logic.name,
                    Left     = new SimpleConditionExpr { Source = first,  Name = first.name },
                    Right    = right ? new SimpleConditionExpr { Source = right, Name = right.name } : null,
                    Overflow = CollectConditionOverflow(right)
                };
            }

            // 단순 조건
            if (first.Category == BlockCategory.Condition)
                return new SimpleConditionExpr { Source = first, Name = first.name };

            return null;
        }

        /// <summary>
        /// 조건 블록의 ConditionOut에 이어 붙은 다음 블록을 반환한다 (없으면 null).
        /// </summary>
        private static CodingBlock NextInConditionChain(CodingBlock block)
        {
            ConditionOutSocket condOut = block ? block.GetSocket<ConditionOutSocket>() : null;
            return condOut ? condOut.Occupant : null;
        }

        /// <summary>
        /// 두 번째 조건 뒤에 더 이어 붙인 블록을 모두 모은다 (없으면 null).
        /// </summary>
        private static CodingBlock[] CollectConditionOverflow(CodingBlock right)
        {
            CodingBlock next = NextInConditionChain(right);
            if (!next) return null;

            List<CodingBlock> overflow = new List<CodingBlock>();
            while (next)
            {
                overflow.Add(next);
                next = NextInConditionChain(next);
            }
            return overflow.ToArray();
        }

        /// <summary>
        /// 만약 블록 중 조건이 연결되지 않은 첫 번째 블록을 찾는다 (중첩 본문 포함).
        /// </summary>
        private static CodingBlock FindIfWithoutCondition(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is IfInstruction ifInstr && ifInstr.Condition is null)
                    return ifInstr.Source;
            }
            return null;
        }

        /// <summary>
        /// 만약 블록 조건에 '그리고/또는'(Logic) 블록이 연결됐지만 두 번째 조건이 없는 첫 번째 블록을 찾는다 (중첩 본문 포함).
        /// </summary>
        private static CodingBlock FindLogicWithoutRight(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is IfInstruction ifInstr && ifInstr.Condition is LogicConditionExpr logic && logic.Right is null)
                    return logic.Source;
            }
            return null;
        }

        /// <summary>
        /// 만약 블록 조건에 두 번째 조건 뒤로 더 이어 붙인 블록이 있으면 그 블록들을 반환한다 (중첩 본문 포함, 없으면 null).
        /// </summary>
        private static CodingBlock[] FindConditionOverflow(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is IfInstruction ifInstr && ifInstr.Condition is LogicConditionExpr logic && logic.Overflow is not null)
                    return logic.Overflow;
            }
            return null;
        }

        /// <summary>
        /// 내부가 비어 있는 제어 블록(반복하기/만약/아니면)을 찾는다.
        /// </summary>
        private static CodingBlock FindFlowControlWithEmptyInner(List<BlockInstruction> instructions)
        {
            // 아니면은 놓았을 때만 검사한다 — 아니면 블록 없이 만약만 쓰는 것은 허용한다.
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                switch (instr)
                {
                    case RepeatInstruction rep when rep.Body is null || rep.Body.Count == 0:
                        return rep.Source;
                    case IfInstruction ifInstr when ifInstr.Then is null || ifInstr.Then.Count == 0:
                        return ifInstr.Source;
                    case IfInstruction ifInstr when ifInstr.HasElseMarker && (ifInstr.Else is null || ifInstr.Else.Count == 0):
                        return ifInstr.ElseMarkerSource;
                }
            }
            return null;
        }

        /// <summary>
        /// 코딩 존에 있는 '아니면' 블록 중 '만약' 블록의 Inner 체인 안에서 실제로 만나지 못한 것을 찾는다.
        /// (WalkInnerWithElse가 순회 중 방문한 블록만 유효 — 그 외는 메인 체인/반복문 안/미연결 등 잘못된 위치)
        /// </summary>
        private static CodingBlock[] FindElseOutsideIf(CompileContext ctx)
        {
            List<CodingBlock> misplaced = new List<CodingBlock>();
            foreach (CodingBlock b in ctx.AllBlocks)
            {
                if (!IsActiveInZone(ctx.Zone, b)) continue;
                if (b.Category == BlockCategory.Else && !ctx.VisitedElseBlocks.Contains(b))
                    misplaced.Add(b);
            }
            return misplaced.ToArray();
        }

        /// <summary>
        /// 반복하기 블록의 헤더에 달린 Value 블록에서 횟수를 읽는다.
        /// </summary>
        private static int ReadRepeatCount(CodingBlock block, out CodingBlock valueSource)
        {
            // 값이 없으면 무한 반복(-1) — 실제 실행은 BlockExecutor가 1회로 제한한다.
            ValueOutSocket vos = block.GetSocket<ValueOutSocket>();
            if (vos && vos.Occupant && int.TryParse(vos.Occupant.name, out int n))
            {
                valueSource = vos.Occupant;
                return n;
            }
            valueSource = null;
            return -1;
        }

        /// <summary>
        /// 프로그램에 연결된 함수(호출) 블록을 찾는다 (없으면 null).
        /// </summary>
        private static CodingBlock FindFunctionCall(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
                if (instr is FunctionInstruction fn) return fn.Source;
            return null;
        }

        /// <summary>
        /// 함수 정의 블록 안에 놓인 함수 호출 블록을 찾는다 — 메인 체인이 함수를 부르지 않아 정의를 펼치지 않은 경우도 잡는다(없으면 null).
        /// </summary>
        private static CodingBlock FindFunctionCallInsideDef(CompileContext ctx)
        {
            if (ctx.FunctionCallInsideDef) return ctx.FunctionCallInsideDef;

            CodingBlock funcDef = FindSceneBlock(ctx, BlockCategory.FunctionDef);
            if (!funcDef) return null;

            foreach (CodingBlock b in ctx.AllBlocks)
                if (b && b.Category == BlockCategory.Function && b.transform.IsChildOf(funcDef.transform)) return b;
            return null;
        }

        /// <summary>
        /// 씬(코딩존·인벤토리 포함)에서 지정 카테고리의 첫 블록을 반환한다.
        /// </summary>
        private static CodingBlock FindSceneBlock(CompileContext ctx, BlockCategory cat)
        {
            foreach (CodingBlock b in ctx.AllBlocks)
                if (b && b.Category == cat) return b;
            return null;
        }
    }
}
