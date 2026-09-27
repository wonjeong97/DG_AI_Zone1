using System.Collections.Generic;
using Cysharp.Text;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// 컴파일 실패 사유 분류. 호출부가 에러 메시지 문자열을 비교하지 않고 분기할 수 있게 한다.
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
        // WalkInnerWithElse가 '만약' 블록 안에서 실제로 만난 '아니면' 블록 — Compile() 1회 호출 동안만 유효
        private static readonly HashSet<CodingBlock> _visitedElseBlocks = new();

        // 이번 컴파일에서 검사할 블록 전체(코딩 패널 + 인벤토리, 비활성 탭 포함) — CodingZone이 관리하는 목록
        private static IReadOnlyList<CodingBlock> _allBlocks = System.Array.Empty<CodingBlock>();

        /// <summary>
        /// 코딩 패널의 블록 연결을 시작하기부터 순회해 명령 목록으로 만들고, 배치 규칙을 검증한다.
        /// </summary>
        public static CompileResult Compile(CodingZone zone)
        {
            _visitedElseBlocks.Clear();
            _allBlocks = zone ? zone.Blocks : System.Array.Empty<CodingBlock>();

            // '시작하기' 블록은 소켓 계층 어디에나 있을 수 있으므로 패널 위 블록 전체에서 찾는다
            CodingBlock start = null;
            foreach (CodingBlock b in _allBlocks)
            {
                if (!IsActiveInZone(zone, b)) continue;
                if (b.Category == BlockCategory.Control && b.ControlRole == Data.ControlRole.Start)
                { start = b; break; }
            }
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
            CodingBlock terminal = WalkChain(socket.Occupant, program);
            bool reachedEnd = terminal && terminal.ControlRole == Data.ControlRole.End;

            // 레벨 5(함수) 전용 규칙 — 함수/함수 정의 블록 필수 사용 + 함수 정의는 1개 이상 내부 블록
            if (CodingBlock.RestrictMainChainToFunction)
            {
                if (!FindMainChainFunction(start))
                    return CompileResult.Fail(Constants.CompilerMessages.FunctionBetweenStartEnd,
                        FindSceneBlock(BlockCategory.Function), program, reachedEnd);

                CodingBlock funcDef = FindSceneBlock(BlockCategory.FunctionDef);
                if (!funcDef || !FunctionDefHasInnerBlock(funcDef))
                    return CompileResult.Fail(Constants.CompilerMessages.EmptyFunctionDef, funcDef, program, reachedEnd);
            }

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

            CodingBlock emptyFlow = FindFlowControlWithEmptyInner(program);
            if (emptyFlow)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.EmptyFlowInnerFormat, emptyFlow.name),
                    emptyFlow, program, reachedEnd);

            CodingBlock controlInInner = FindControlInInner(program);
            if (controlInInner)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.ControlInsideFlowFormat, controlInInner.name),
                    controlInInner, program, reachedEnd);

            // 코딩 존에 있는 '아니면' 블록 중 '만약' 블록 안에 놓이지 않은 것 — 빨간 외곽선 표시
            CodingBlock[] misplacedElse = FindElseOutsideIf(zone);
            if (misplacedElse.Length > 0)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.ElseOutsideIfFormat, misplacedElse.Length),
                    misplacedElse, program, reachedEnd);

            // 씬의 모든 실행 블록(Command, FlowControl 등 — 인벤토리·방치 블록 포함)이 프로그램에 포함돼야 함
            CodingBlock[] unused = FindUnusedExecutableBlocks(program);
            if (unused.Length > 0)
                return CompileResult.Fail(
                    ZString.Format(Constants.CompilerMessages.UnusedBlocksFormat, unused.Length),
                    unused, program, reachedEnd, CompileErrorKind.UnusedBlocks);

            if (!reachedEnd)
            {
                // 씬 전체(인벤토리 포함)에서 완성하기 블록을 찾아 표시
                CodingBlock endBlock = null;
                foreach (CodingBlock b in _allBlocks)
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
        /// ChainOutSocket.Occupant를 따라 체인을 순회한다.
        /// Control 블록(완성하기)에 도달하면 그 블록을 반환하고, 체인이 끊기면 null을 반환한다.
        /// </summary>
        private static CodingBlock WalkChain(CodingBlock current, List<BlockInstruction> output)
        {
            while (current)
            {
                if (current.Category == BlockCategory.Control) return current;

                // 함수(사용) 블록 — 씬의 함수 정의 블록 내부 블록들을 읽어 FunctionInstruction으로 포장
                if (current.Category == BlockCategory.Function)
                {
                    List<BlockInstruction> body = new List<BlockInstruction>();
                    string defName = ExpandFunctionCall(body);
                    output.Add(new FunctionInstruction
                    {
                        Source = current,
                        Name = current.name,
                        DefName = defName ?? Constants.CategoryNames.FunctionDef,
                        Body = body
                    });
                }
                else
                {
                    BlockInstruction instr = Build(current);
                    if (instr is not null) output.Add(instr);
                }

                ChainOutSocket socket = ChainOutSocket.OfBlock(current);
                current = socket ? socket.Occupant : null;
            }
            return null;
        }

        /// <summary>
        /// 함수 정의(FunctionDef) 블록의 Inner 컨테이너에 배치된 블록들을 순서대로 읽어 프로그램에 펼친다.
        /// (프로토타입 — 함수는 1개 가정: 씬에서 첫 FunctionDef 블록을 사용)
        /// </summary>
        private static string ExpandFunctionCall(List<BlockInstruction> output)
        {
            CodingBlock funcDef = FindSceneBlock(BlockCategory.FunctionDef);
            if (!funcDef) return null;

            List<InnerSocket> inners = new List<InnerSocket>();
            funcDef.GetSockets(inners);
            foreach (InnerSocket inner in inners)
                WalkInner(inner, output);

            return funcDef.name;
        }

        /// <summary>
        /// 블록 카테고리에 맞는 명령 노드를 만든다 (명령이 아닌 블록은 null).
        /// </summary>
        private static BlockInstruction Build(CodingBlock block)
        {
            return block.Category switch
            {
                BlockCategory.Command         => BuildCommand(block),
                BlockCategory.Action          => new ActionInstruction         { Source = block, Action  = block.name },
                BlockCategory.ConditionAction => new ConditionActionInstruction{ Source = block, Action  = block.name },
                BlockCategory.FlowControl     => BuildFlowControl(block),
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
        private static BlockInstruction BuildFlowControl(CodingBlock block)
        {
            // FlowControl 블록의 Inner 컨테이너 진입 소켓 — 등록 순서가 배치 순서(첫 번째가 본문)
            List<InnerSocket> inners = new List<InnerSocket>();
            block.GetSockets(inners);

            bool isRepeat = block.name.Contains(Constants.BlockLabels.RepeatKeyword);

            if (isRepeat)
            {
                List<BlockInstruction> body = new List<BlockInstruction>();
                if (inners.Count > 0) WalkInner(inners[0], body);
                int count = ReadRepeatCount(block, out CodingBlock countValueSource);
                return new RepeatInstruction
                {
                    Source      = block,
                    Count       = count,
                    Body        = body,
                    ValueSource = countValueSource
                };
            }
            else // 만약 (if)
            {
                List<BlockInstruction> then = new List<BlockInstruction>();
                List<BlockInstruction> els  = new List<BlockInstruction>();
                CodingBlock elseMarker = inners.Count > 0 ? WalkInnerWithElse(inners[0], then, els) : null;
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
        }

        /// <summary>
        /// 만약 블록의 Inner 체인을 탐색한다 — "아니면" 블록을 만나면 그 이후 블록부터는 els로 출력 대상을 전환한다.
        /// "아니면" 블록 자신은 구분 표시일 뿐이라 어느 분기에도 포함되지 않으며,
        /// 실제로 만난 "아니면" 블록을 반환한다 (없으면 null — els가 비어 있어도 마커가 있었는지 구분하는 용도).
        /// </summary>
        private static CodingBlock WalkInnerWithElse(InnerSocket innerSocket, List<BlockInstruction> then, List<BlockInstruction> els)
        {
            CodingBlock first = innerSocket ? innerSocket.Occupant : null;
            if (!first) return null;

            CodingBlock elseMarker = null;
            List<BlockInstruction> output = then;
            CodingBlock current = first;
            while (current)
            {
                if (current.Category == BlockCategory.Control) break;

                if (current.Category == BlockCategory.Else)
                {
                    _visitedElseBlocks.Add(current);
                    elseMarker = current;
                    output = els;
                }
                else if (current.Category == BlockCategory.Function)
                {
                    List<BlockInstruction> body = new List<BlockInstruction>();
                    string defName = ExpandFunctionCall(body);
                    output.Add(new FunctionInstruction
                    {
                        Source = current,
                        Name = current.name,
                        DefName = defName ?? Constants.CategoryNames.FunctionDef,
                        Body = body
                    });
                }
                else
                {
                    BlockInstruction instr = Build(current);
                    if (instr is not null) output.Add(instr);
                }

                ChainOutSocket socket = ChainOutSocket.OfBlock(current);
                current = socket ? socket.Occupant : null;
            }

            return elseMarker;
        }

        /// <summary>
        /// Inner 컨테이너 내부를 탐색한다.
        /// 진입 소켓에 연결된 블록이 체인 소켓을 가지면 체인을 따라가고(사용자가 연결한 경우),
        /// 아니면 컨테이너 직속 자식 블록을 순서대로 읽는다(초기 배치된 블록).
        /// </summary>
        private static void WalkInner(InnerSocket innerSocket, List<BlockInstruction> output)
        {
            if (!innerSocket) return;

            Transform container = innerSocket.transform.parent;
            CodingBlock first = innerSocket.Occupant;
            if (!first && container)
            {
                foreach (Transform child in container)
                {
                    if (child.TryGetComponent<CodingBlock>(out CodingBlock b) && b.Category != BlockCategory.Control)
                    { first = b; break; }
                }
            }
            if (!first) return;

            if (ChainOutSocket.OfBlock(first))
            {
                _ = WalkChain(first, output);
            }
            else if (container)
            {
                foreach (Transform child in container)
                {
                    if (!child.TryGetComponent<CodingBlock>(out CodingBlock b)) continue;
                    if (b.Category == BlockCategory.Control) continue;
                    BlockInstruction instr = Build(b);
                    if (instr is not null) output.Add(instr);
                }
            }
        }

        /// <summary>
        /// Command 블록 중 Value가 연결되지 않은 첫 번째 블록을 찾는다 (중첩 본문 포함).
        /// ValueKind.None(값 슬롯 없는 동작 블록)은 검사 대상에서 제외한다.
        /// </summary>
        private static CodingBlock FindCommandWithoutValue(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is CommandInstruction cmd && cmd.Value is null
                    && cmd.Source && cmd.Source.ValueKind != ValueKind.None)
                    return cmd.Source;
            }
            return null;
        }

        /// <summary>
        /// 프로그램에 포함되지 않은 실행 블록(Command, FlowControl 등)을 찾는다 — 인벤토리·코딩존 방치 블록 모두 대상.
        /// </summary>
        private static CodingBlock[] FindUnusedExecutableBlocks(List<BlockInstruction> program)
        {
            HashSet<CodingBlock> used = new HashSet<CodingBlock>();
            foreach (BlockInstruction instr in InstructionTree.Traverse(program))
                if (instr.Source) used.Add(instr.Source);

            List<CodingBlock> unused = new List<CodingBlock>();
            foreach (CodingBlock b in _allBlocks)
            {
                if (b && IsExecutable(b.Category) && !used.Contains(b))
                    unused.Add(b);
            }
            return unused.ToArray();
        }

        /// <summary>
        /// 실행 흐름에 반드시 편입돼야 하는 카테고리인지 확인한다 (값·조건·제어 블록은 단독으로 남아도 무방).
        /// </summary>
        private static bool IsExecutable(BlockCategory category) =>
            category is BlockCategory.Command
                     or BlockCategory.FlowControl
                     or BlockCategory.Action
                     or BlockCategory.ConditionAction;

        /// <summary>
        /// FlowControl(만약) 블록의 헤더 조건 슬롯에서 조건식을 읽는다.
        /// 체인: [조건1] -ConditionOut→ConditionIn- [그리고/또는] -ConditionOut→ConditionIn- [조건2]
        /// </summary>
        private static ConditionExpr BuildConditionExpr(CodingBlock flowBlock)
        {
            ValueOutSocket vos = flowBlock.GetSocket<ValueOutSocket>();
            if (!vos || !vos.Occupant) return null;

            CodingBlock first = vos.Occupant;

            // 조건1의 ConditionOutSocket에 Logic 블록이 연결됐는지 확인
            ConditionOutSocket firstCondOut = first.GetSocket<ConditionOutSocket>();

            if (firstCondOut && firstCondOut.Occupant &&
                firstCondOut.Occupant.Category == BlockCategory.Logic)
            {
                CodingBlock logic = firstCondOut.Occupant;
                ConditionOutSocket logicCondOut = logic.GetSocket<ConditionOutSocket>();

                CodingBlock right = logicCondOut ? logicCondOut.Occupant : null;
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
        /// 내부가 비어 있는 제어 블록(반복하기/만약)을 찾는다.
        /// 만약 블록은 Then만 검사한다 — else 분기는 비워 둘 수 있다.
        /// </summary>
        private static CodingBlock FindFlowControlWithEmptyInner(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                switch (instr)
                {
                    case RepeatInstruction rep when rep.Body is null || rep.Body.Count == 0:
                        return rep.Source;
                    case IfInstruction ifInstr when ifInstr.Then is null || ifInstr.Then.Count == 0:
                        return ifInstr.Source;
                }
            }
            return null;
        }

        /// <summary>
        /// 제어 블록(반복/만약/함수 정의) 내부에 Control 블록(시작하기/완성하기)이 들어있는지 찾는다.
        /// 최상위 명령 자체는 대상이 아니므로 하위 본문만 Traverse로 훑는다.
        /// </summary>
        private static CodingBlock FindControlInInner(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in instructions)
            {
                foreach (List<BlockInstruction> body in InstructionTree.ChildBodies(instr))
                {
                    foreach (BlockInstruction nested in InstructionTree.Traverse(body))
                    {
                        if (nested.Source && nested.Source.Category == BlockCategory.Control)
                            return nested.Source;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 코딩 존에 있는 '아니면' 블록 중 '만약' 블록의 Inner 체인 안에서 실제로 만나지 못한 것을 찾는다.
        /// (WalkInnerWithElse가 순회 중 방문한 블록만 유효 — 그 외는 메인 체인/반복문 안/미연결 등 잘못된 위치)
        /// </summary>
        private static CodingBlock[] FindElseOutsideIf(CodingZone zone)
        {
            List<CodingBlock> misplaced = new List<CodingBlock>();
            foreach (CodingBlock b in _allBlocks)
            {
                if (!IsActiveInZone(zone, b)) continue;
                if (b.Category == BlockCategory.Else && !_visitedElseBlocks.Contains(b))
                    misplaced.Add(b);
            }
            return misplaced.ToArray();
        }

        /// <summary>
        /// 반복하기 블록의 헤더에 달린 Value 블록에서 횟수를 읽는다.
        /// 값이 없으면 무한 반복(-1) — 실제 실행은 BlockExecutor가 1회로 제한한다.
        /// </summary>
        private static int ReadRepeatCount(CodingBlock block, out CodingBlock valueSource)
        {
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
        /// 시작하기 체인을 따라가며 함수(사용) 블록을 찾는다 (완성하기 도달 시 중단).
        /// </summary>
        private static CodingBlock FindMainChainFunction(CodingBlock start)
        {
            ChainOutSocket socket = ChainOutSocket.OfBlock(start);
            CodingBlock current = socket ? socket.Occupant : null;
            while (current)
            {
                if (current.Category == BlockCategory.Control) break;
                if (current.Category == BlockCategory.Function) return current;
                socket = ChainOutSocket.OfBlock(current);
                current = socket ? socket.Occupant : null;
            }
            return null;
        }

        /// <summary>
        /// 씬(코딩존·인벤토리 포함)에서 지정 카테고리의 첫 블록을 반환한다.
        /// </summary>
        private static CodingBlock FindSceneBlock(BlockCategory cat)
        {
            foreach (CodingBlock b in _allBlocks)
                if (b && b.Category == cat) return b;
            return null;
        }

        /// <summary>
        /// 함수 정의 블록의 Inner 컨테이너에 블록이 1개 이상 있는지 확인한다.
        /// </summary>
        private static bool FunctionDefHasInnerBlock(CodingBlock funcDef)
        {
            List<InnerSocket> inners = new List<InnerSocket>();
            funcDef.GetSockets(inners);
            foreach (InnerSocket inner in inners)
                if (inner.Occupant) return true;
            return false;
        }
    }
}
