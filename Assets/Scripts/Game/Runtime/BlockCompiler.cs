using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    public readonly struct CompileResult
    {
        public readonly bool Success;
        public readonly List<BlockInstruction> Instructions;
        public readonly string Error;
        public readonly CodingBlock[] ErrorBlocks;

        // 검증 성공/실패와 무관하게 시작하기 이후 순회된 프로그램 (로그·코드 표시용, 순회 전 실패면 null)
        public readonly List<BlockInstruction> Program;

        // 체인이 완성하기(End) 블록까지 도달했는지 — 코드 표시 시 END 출력 여부 판단용
        public readonly bool ReachedEnd;

        private CompileResult(bool success, List<BlockInstruction> instr, string err, CodingBlock[] errorBlocks,
            List<BlockInstruction> program, bool reachedEnd)
        { Success = success; Instructions = instr; Error = err; ErrorBlocks = errorBlocks; Program = program; ReachedEnd = reachedEnd; }

        // 성공은 항상 완성하기에 도달한 상태
        public static CompileResult Ok(List<BlockInstruction> instr) => new(true, instr, null, null, instr, true);

        public static CompileResult Fail(string err, CodingBlock block = null,
            List<BlockInstruction> program = null, bool reachedEnd = false)
            => new(false, null, err, block ? new[] { block } : null, program, reachedEnd);

        public static CompileResult Fail(string err, CodingBlock[] blocks,
            List<BlockInstruction> program = null, bool reachedEnd = false)
            => new(false, null, err, blocks, program, reachedEnd);
    }

    public static class BlockCompiler
    {
        public static CompileResult Compile(CodingZone zone)
        {
            // '시작하기' 블록은 소켓 계층 어디에나 있을 수 있으므로 전체 탐색
            CodingBlock start = null;
            foreach (CodingBlock b in zone.GetComponentsInChildren<CodingBlock>())
            {
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

            // transform.Find: 직접 자식만 탐색 — 하위 체인 소켓과 혼동 방지
            ChainOutSocket socket = null;
            start.transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out socket);
            if (!socket || !socket.Occupant)
                return CompileResult.Fail(Constants.CompilerMessages.MissingConnectedAfterStart, start);

            var program = new List<BlockInstruction>();
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
                return CompileResult.Fail($"'{cmdError.name}' 블록에 값 블록이 없습니다", cmdError, program, reachedEnd);

            CodingBlock condError = FindIfWithoutCondition(program);
            if (condError)
                return CompileResult.Fail($"'{condError.name}' 블록에 조건이 없습니다", condError, program, reachedEnd);

            CodingBlock emptyFlow = FindFlowControlWithEmptyInner(program);
            if (emptyFlow)
                return CompileResult.Fail($"'{emptyFlow.name}' 블록 내부에 최소 1개의 블록이 있어야 합니다", emptyFlow, program, reachedEnd);

            CodingBlock controlInInner = FindControlInInner(program);
            if (controlInInner)
                return CompileResult.Fail($"'{controlInInner.name}' 블록은 제어 블록 내부에 넣을 수 없습니다", controlInInner, program, reachedEnd);

            // 씬의 모든 실행 블록(Command, FlowControl 등 — 인벤토리·방치 블록 포함)이 프로그램에 포함돼야 함
            CodingBlock[] unused = FindUnusedExecutableBlocks(program);
            if (unused.Length > 0)
                return CompileResult.Fail($"사용되지 않은 블록이 있습니다 ({unused.Length}개)", unused, program, reachedEnd);

            if (!reachedEnd)
            {
                // 씬 전체(인벤토리 포함)에서 완성하기 블록을 찾아 표시
                CodingBlock endBlock = null;
                foreach (CodingBlock b in FindAllBlocksInScene())
                    if (b.ControlRole == Data.ControlRole.End) { endBlock = b; break; }
                return CompileResult.Fail(Constants.CompilerMessages.MissingEndBlock, endBlock, program, reachedEnd);
            }

            return CompileResult.Ok(program);
        }

        // ChainOutSocket.Occupant 포인터를 따라 체인을 순회.
        // Control 블록(완성하기)에 도달하면 그 블록을 반환, 체인이 끊기면 null 반환.
        private static CodingBlock WalkChain(CodingBlock current, List<BlockInstruction> output)
        {
            while (current)
            {
                if (current.Category == BlockCategory.Control) return current;

                // 함수(사용) 블록 — 씬의 함수 정의 블록 내부 블록들을 읽어 FunctionInstruction으로 포장
                if (current.Category == BlockCategory.Function)
                {
                    var body = new List<BlockInstruction>();
                    string defName = ExpandFunctionCall(body);
                    output.Add(new FunctionInstruction
                    {
                        Source = current,
                        Name = current.name,
                        DefName = defName ?? "함수 정의",
                        Body = body
                    });
                }
                else
                {
                    BlockInstruction instr = Build(current);
                    if (instr is not null) output.Add(instr);
                }

                ChainOutSocket socket = null;
                current.transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out socket);
                current = socket?.Occupant;
            }
            return null;
        }

        // 함수 정의(FunctionDef) 블록의 Inner 컨테이너에 배치된 블록들을 순서대로 읽어 프로그램에 펼친다.
        // (프로토타입 — 함수는 1개 가정: 씬에서 첫 FunctionDef 블록을 사용)
        private static string ExpandFunctionCall(List<BlockInstruction> output)
        {
            CodingBlock funcDef = null;
            foreach (CodingBlock b in FindAllBlocksInScene())
                if (b.Category == BlockCategory.FunctionDef) { funcDef = b; break; }
            if (!funcDef) return null;

            foreach (Transform child in funcDef.transform)
                if (child.name.StartsWith("Inner"))
                    WalkInner(child, output);

            return funcDef.name;
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
            block.transform.Find(Constants.Sockets.ValueOutName)?.TryGetComponent(out vos);
            CodingBlock occupant = vos ? vos.Occupant : null;
            return new CommandInstruction
            {
                Source    = block,
                Command   = block.name,
                Value     = occupant ? occupant.name : null,
                ValueKind = occupant ? occupant.ValueKind : ValueKind.None
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
            InnerSocket innerSocket = inner.GetComponentInChildren<InnerSocket>();
            if (innerSocket && innerSocket.Occupant)
            {
                first = innerSocket.Occupant;
            }
            else
            {
                foreach (Transform child in inner)
                {
                    if (child.TryGetComponent<CodingBlock>(out CodingBlock b) && b.Category != BlockCategory.Control)
                    { first = b; break; }
                }
            }
            if (!first) return;

            if (first.transform.Find(Constants.Sockets.ChainOutName))
            {
                _ = WalkChain(first, output);
            }
            else
            {
                foreach (Transform child in inner)
                {
                    if (!child.TryGetComponent<CodingBlock>(out CodingBlock b)) continue;
                    if (b.Category == BlockCategory.Control) continue;
                    BlockInstruction instr = Build(b);
                    if (instr is not null) output.Add(instr);
                }
            }
        }

        // Command 블록 중 Value가 연결되지 않은 첫 번째 블록을 재귀적으로 탐색
        // (ValueKind.None = 값 슬롯 없는 동작 블록은 검사 대상에서 제외)
        private static CodingBlock FindCommandWithoutValue(List<BlockInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr is CommandInstruction cmd && cmd.Value is null
                    && cmd.Source && cmd.Source.ValueKind != ValueKind.None)
                    return cmd.Source;
                if (instr is RepeatInstruction rep && rep.Body is not null)
                {
                    CodingBlock err = FindCommandWithoutValue(rep.Body);
                    if (err) return err;
                }
                if (instr is FunctionInstruction fn && fn.Body is not null)
                {
                    CodingBlock err = FindCommandWithoutValue(fn.Body);
                    if (err) return err;
                }
                if (instr is IfInstruction ifInstr)
                {
                    if (ifInstr.Then is not null) { CodingBlock err = FindCommandWithoutValue(ifInstr.Then); if (err) return err; }
                    if (ifInstr.Else is not null) { CodingBlock err = FindCommandWithoutValue(ifInstr.Else); if (err) return err; }
                }
            }
            return null;
        }

        // 프로그램에 포함되지 않은 실행 블록(Command, FlowControl 등) 탐색 — 인벤토리·코딩존 방치 블록 모두 대상
        private static CodingBlock[] FindUnusedExecutableBlocks(List<BlockInstruction> program)
        {
            var used = new HashSet<CodingBlock>();
            CollectProgramSources(program, used);

            var unused = new List<CodingBlock>();
            foreach (CodingBlock b in FindAllBlocksInScene())
            {
                bool isExecutable = b.Category == BlockCategory.Command ||
                                    b.Category == BlockCategory.FlowControl ||
                                    b.Category == BlockCategory.Action ||
                                    b.Category == BlockCategory.ConditionAction;

                if (isExecutable && !used.Contains(b))
                    unused.Add(b);
            }
            return unused.ToArray();
        }

        private static void CollectProgramSources(List<BlockInstruction> instructions, HashSet<CodingBlock> used)
        {
            foreach (var instr in instructions)
            {
                if (instr.Source) used.Add(instr.Source);

                switch (instr)
                {
                    case RepeatInstruction rep when rep.Body is not null:
                        CollectProgramSources(rep.Body, used);
                        break;
                    case FunctionInstruction fn when fn.Body is not null:
                        CollectProgramSources(fn.Body, used);
                        break;
                    case IfInstruction ifInstr:
                        if (ifInstr.Then is not null) CollectProgramSources(ifInstr.Then, used);
                        if (ifInstr.Else is not null) CollectProgramSources(ifInstr.Else, used);
                        break;
                }
            }
        }

        // FlowControl(만약) 블록의 헤더 조건 슬롯에서 ConditionExpr를 읽음
        // 체인: [조건1] -ConditionOut→ConditionIn- [그리고/또는] -ConditionOut→ConditionIn- [조건2]
        private static ConditionExpr BuildConditionExpr(CodingBlock flowBlock)
        {
            ValueOutSocket vos = null;
            foreach (Transform child in flowBlock.transform)
            {
                if (!child.name.StartsWith("Header_")) continue;
                child.Find(Constants.Sockets.ValueOutName)?.TryGetComponent(out vos);
                if (vos) break;
            }

            if (!vos || !vos.Occupant) return null;

            CodingBlock first = vos.Occupant;

            // 조건1의 ConditionOutSocket에 Logic 블록이 연결됐는지 확인
            ConditionOutSocket firstCondOut = null;
            first.transform.Find(Constants.Sockets.ConditionOutName)?.TryGetComponent(out firstCondOut);

            if (firstCondOut && firstCondOut.Occupant &&
                firstCondOut.Occupant.Category == BlockCategory.Logic)
            {
                CodingBlock logic = firstCondOut.Occupant;
                ConditionOutSocket logicCondOut = null;
                logic.transform.Find(Constants.Sockets.ConditionOutName)?.TryGetComponent(out logicCondOut);

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
                    if (ifInstr.Condition is null) return ifInstr.Source;
                    if (ifInstr.Then is not null) { CodingBlock err = FindIfWithoutCondition(ifInstr.Then); if (err) return err; }
                    if (ifInstr.Else is not null) { CodingBlock err = FindIfWithoutCondition(ifInstr.Else); if (err) return err; }
                }
                else if (instr is RepeatInstruction rep && rep.Body is not null)
                {
                    CodingBlock err = FindIfWithoutCondition(rep.Body);
                    if (err) return err;
                }
                else if (instr is FunctionInstruction fn && fn.Body is not null)
                {
                    CodingBlock err = FindIfWithoutCondition(fn.Body);
                    if (err) return err;
                }
            }
            return null;
        }

        // 제어 블록(반복하기/만약) 내부에 최소 1개의 블록이 있는지 확인
        private static CodingBlock FindFlowControlWithEmptyInner(List<BlockInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr is RepeatInstruction rep)
                {
                    if (rep.Body is null || rep.Body.Count == 0)
                        return rep.Source;
                    CodingBlock err = FindFlowControlWithEmptyInner(rep.Body);
                    if (err) return err;
                }
                else if (instr is IfInstruction ifInstr)
                {
                    if (ifInstr.Then is null || ifInstr.Then.Count == 0)
                        return ifInstr.Source;
                    if (ifInstr.Then is not null) { CodingBlock err = FindFlowControlWithEmptyInner(ifInstr.Then); if (err) return err; }
                    if (ifInstr.Else is not null) { CodingBlock err = FindFlowControlWithEmptyInner(ifInstr.Else); if (err) return err; }
                }
                else if (instr is FunctionInstruction fn && fn.Body is not null)
                {
                    CodingBlock err = FindFlowControlWithEmptyInner(fn.Body);
                    if (err) return err;
                }
            }
            return null;
        }

        // 제어 블록 내부에 Control 블록(시작하기/완성하기)이 포함되어 있는지 확인
        private static CodingBlock FindControlInInner(List<BlockInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr is RepeatInstruction rep && rep.Body is not null)
                {
                    foreach (var innerInstr in rep.Body)
                    {
                        if (innerInstr.Source && innerInstr.Source.Category == BlockCategory.Control)
                            return innerInstr.Source;
                    }
                    CodingBlock err = FindControlInInner(rep.Body);
                    if (err) return err;
                }
                else if (instr is IfInstruction ifInstr)
                {
                    if (ifInstr.Then is not null)
                    {
                        foreach (var innerInstr in ifInstr.Then)
                        {
                            if (innerInstr.Source && innerInstr.Source.Category == BlockCategory.Control)
                                return innerInstr.Source;
                        }
                        CodingBlock err = FindControlInInner(ifInstr.Then);
                        if (err) return err;
                    }
                    if (ifInstr.Else is not null)
                    {
                        foreach (var innerInstr in ifInstr.Else)
                        {
                            if (innerInstr.Source && innerInstr.Source.Category == BlockCategory.Control)
                                return innerInstr.Source;
                        }
                        CodingBlock err = FindControlInInner(ifInstr.Else);
                        if (err) return err;
                    }
                }
                else if (instr is FunctionInstruction fn && fn.Body is not null)
                {
                    foreach (var innerInstr in fn.Body)
                    {
                        if (innerInstr.Source && innerInstr.Source.Category == BlockCategory.Control)
                            return innerInstr.Source;
                    }
                    CodingBlock err = FindControlInInner(fn.Body);
                    if (err) return err;
                }
            }
            return null;
        }

        // 반복하기 블록의 헤더에 달린 Value 블록에서 횟수를 읽음
        // 값이 없으면 무한 반복(-1) — 실제 실행은 BlockExecutor가 1회로 제한
        private static int ReadRepeatCount(CodingBlock block)
        {
            foreach (Transform child in block.transform)
            {
                if (!child.name.StartsWith("Header_")) continue;
                ValueOutSocket vos = null;
                child.Find(Constants.Sockets.ValueOutName)?.TryGetComponent(out vos);
                if (vos && vos.Occupant && int.TryParse(vos.Occupant.name, out int n))
                    return n;
            }
            return -1;
        }

        // 시작하기 체인을 따라가며 함수(사용) 블록을 찾음 (완성하기 도달 시 중단)
        private static CodingBlock FindMainChainFunction(CodingBlock start)
        {
            ChainOutSocket socket = null;
            start.transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out socket);
            CodingBlock current = socket?.Occupant;
            while (current)
            {
                if (current.Category == BlockCategory.Control) break;
                if (current.Category == BlockCategory.Function) return current;
                socket = null;
                current.transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out socket);
                current = socket?.Occupant;
            }
            return null;
        }

        // 씬(코딩존·인벤토리 포함)에서 지정 카테고리의 첫 블록을 반환
        private static CodingBlock FindSceneBlock(BlockCategory cat)
        {
            foreach (CodingBlock b in FindAllBlocksInScene())
                if (b.Category == cat) return b;
            return null;
        }

        // 함수 정의 블록의 Inner 컨테이너에 블록이 1개 이상 있는지
        private static bool FunctionDefHasInnerBlock(CodingBlock funcDef)
        {
            foreach (Transform child in funcDef.transform)
            {
                if (!child.name.StartsWith("Inner")) continue;
                InnerSocket inner = child.GetComponentInChildren<InnerSocket>();
                if (inner && inner.Occupant) return true;
            }
            return false;
        }

        private static List<CodingBlock> FindAllBlocksInScene()
        {
            var result = new List<CodingBlock>();
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (CodingBlock b in Resources.FindObjectsOfTypeAll<CodingBlock>())
            {
                if (b.gameObject.scene == activeScene)
                {
                    result.Add(b);
                }
            }
            return result;
        }
    }
}
