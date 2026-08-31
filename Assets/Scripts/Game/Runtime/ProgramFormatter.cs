using System.Collections.Generic;
using System.Text;

namespace Game.Runtime
{
    // 컴파일된 명령 목록을 코드 형태의 문자열로 변환한다 (디버그 로그·표시용).
    //   START
    //   태양광 패널의 각도(30도)
    //   반복하기(3) {
    //     ...
    //   }
    //   END
    public static class ProgramFormatter
    {
        private const int IndentSize = 2;
        private const string StartMarker = "START";
        private const string EndMarker = "END";
        private const string UnreachedEndMarker = "(완성하기 미연결)";

        // includeEnd: 체인이 완성하기(End)까지 도달했을 때만 END를 출력. 미연결이면 생략.
        // score: 계산된 최종 점수(컴파일 성공 시에만 값이 있음) — 있으면 코드 뒤에 함께 표시
        public static string ToCode(IReadOnlyList<BlockInstruction> program, bool includeEnd, int? score = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(StartMarker);

            var functions = new List<FunctionInstruction>();

            if (program is not null)
            {
                foreach (BlockInstruction instr in program)
                {
                    Append(sb, instr, 1);
                    CollectFunctions(instr, functions);
                }
            }

            sb.Append(includeEnd ? EndMarker : UnreachedEndMarker);

            if (functions.Count > 0)
            {
                foreach (var fn in functions)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine($"{fn.DefName ?? Constants.CategoryNames.FunctionDef} {{");
                    AppendBody(sb, fn.Body, 1);
                    sb.Append("}");
                }
            }

            if (score.HasValue)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append($"점수: {score.Value}점");
            }

            return sb.ToString();
        }

        // 반복/만약 안에 들어간 함수 호출까지 수집한다.
        // 함수 본문 안의 함수는 수집하지 않는다 — 중첩 함수 정의는 지원 대상이 아니다.
        private static void CollectFunctions(BlockInstruction instr, List<FunctionInstruction> functions)
        {
            if (instr is FunctionInstruction fn)
            {
                functions.Add(fn);
                return;
            }

            foreach (List<BlockInstruction> body in InstructionTree.ChildBodies(instr))
                foreach (BlockInstruction child in body)
                    CollectFunctions(child, functions);
        }

        private static void Append(StringBuilder sb, BlockInstruction instr, int depth)
        {
            string indent = new string(' ', depth * IndentSize);
            switch (instr)
            {
                case CommandInstruction cmd:
                    // 값 슬롯이 있는 명령은 이름(값) — 값 미연결이면 (null), 값 슬롯 없는 명령은 이름만
                    bool hasValueSlot = cmd.Source && cmd.Source.ValueKind != ValueKind.None;
                    sb.AppendLine(hasValueSlot
                        ? $"{indent}{cmd.Command}({cmd.Value ?? "null"})"
                        : $"{indent}{cmd.Command}");
                    break;

                case ActionInstruction act:
                    sb.AppendLine($"{indent}{act.Action}");
                    break;

                case ConditionActionInstruction cond:
                    sb.AppendLine($"{indent}{cond.Action}");
                    break;

                case FunctionInstruction fn:
                    sb.AppendLine($"{indent}{fn.Name ?? Constants.CategoryNames.Function}");
                    break;

                case RepeatInstruction rep:
                    string count = rep.IsInfinite ? "무한" : rep.Count.ToString();
                    sb.AppendLine($"{indent}{Name(rep.Source, Constants.BlockLabels.While)}({count}) {{");
                    AppendBody(sb, rep.Body, depth + 1);
                    sb.AppendLine($"{indent}}}");
                    break;

                case IfInstruction ifInstr:
                    sb.AppendLine($"{indent}{Name(ifInstr.Source, Constants.BlockLabels.If)}({FormatCondition(ifInstr.Condition)}) {{");
                    AppendBody(sb, ifInstr.Then, depth + 1);
                    // '아니면' 블록이 실제로 놓였을 때만 출력 — else가 비어 있어도(뒤에 블록이 없어도) 마커 자체는 표시
                    if (ifInstr.HasElseMarker)
                    {
                        string innerIndent = new string(' ', (depth + 1) * IndentSize);
                        sb.AppendLine($"{innerIndent}{Constants.BlockLabels.Else} {{");
                        AppendBody(sb, ifInstr.Else, depth + 2);
                        sb.AppendLine($"{innerIndent}}}");
                    }
                    sb.AppendLine($"{indent}}}");
                    break;

                case ElseInstruction elseInstr:
                    // '만약' 밖에 잘못 놓인 '아니면' — 컴파일은 실패하지만 실패 시 표시되는 코드에는 제자리에 나타난다
                    sb.AppendLine($"{indent}{Constants.BlockLabels.Else} {{");
                    AppendBody(sb, elseInstr.Body, depth + 1);
                    sb.AppendLine($"{indent}}}");
                    break;
            }
        }

        private static void AppendBody(StringBuilder sb, IReadOnlyList<BlockInstruction> body, int depth)
        {
            if (body is null) return;
            foreach (BlockInstruction instr in body)
                Append(sb, instr, depth);
        }

        private static string FormatCondition(ConditionExpr cond) => cond switch
        {
            SimpleConditionExpr s => s.Name,
            LogicConditionExpr l  => $"{l.Left?.Name} {l.Operator} {l.Right?.Name ?? "null"}",
            _                     => "null"
        };

        private static string Name(CodingBlock block, string fallback) => block ? block.name : fallback;
    }
}
