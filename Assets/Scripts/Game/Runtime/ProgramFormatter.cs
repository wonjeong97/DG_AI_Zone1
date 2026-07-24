using System.Collections.Generic;
using System.Text;

namespace DG.Game.Runtime
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

        // includeEnd: 체인이 완성하기(End)까지 도달했을 때만 END를 출력. 미연결이면 생략.
        public static string ToCode(IReadOnlyList<BlockInstruction> program, bool includeEnd)
        {
            var sb = new StringBuilder();
            sb.AppendLine("START");

            var functions = new List<FunctionInstruction>();

            if (program is not null)
            {
                foreach (BlockInstruction instr in program)
                {
                    Append(sb, instr, 1);
                    CollectFunctions(instr, functions);
                }
            }

            sb.Append(includeEnd ? "END" : "(완성하기 미연결)");

            if (functions.Count > 0)
            {
                foreach (var fn in functions)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine($"{fn.DefName ?? "함수 정의"} {{");
                    AppendBody(sb, fn.Body, 1);
                    sb.Append("}");
                }
            }

            return sb.ToString();
        }

        private static void CollectFunctions(BlockInstruction instr, List<FunctionInstruction> functions)
        {
            if (instr is FunctionInstruction fn)
            {
                functions.Add(fn);
            }
            else if (instr is RepeatInstruction rep && rep.Body is not null)
            {
                foreach (var child in rep.Body) CollectFunctions(child, functions);
            }
            else if (instr is IfInstruction ifInstr)
            {
                if (ifInstr.Then is not null) foreach (var child in ifInstr.Then) CollectFunctions(child, functions);
                if (ifInstr.Else is not null) foreach (var child in ifInstr.Else) CollectFunctions(child, functions);
            }
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
                    sb.AppendLine($"{indent}{fn.Name ?? "함수"}");
                    break;

                case RepeatInstruction rep:
                    string count = rep.IsInfinite ? "무한" : rep.Count.ToString();
                    sb.AppendLine($"{indent}{Name(rep.Source, "반복하기")}({count}) {{");
                    AppendBody(sb, rep.Body, depth + 1);
                    sb.AppendLine($"{indent}}}");
                    break;

                case IfInstruction ifInstr:
                    sb.AppendLine($"{indent}{Name(ifInstr.Source, "만약")}({FormatCondition(ifInstr.Condition)}) {{");
                    AppendBody(sb, ifInstr.Then, depth + 1);
                    sb.AppendLine($"{indent}}}");
                    if (ifInstr.Else is not null && ifInstr.Else.Count > 0)
                    {
                        sb.AppendLine($"{indent}아니면 {{");
                        AppendBody(sb, ifInstr.Else, depth + 1);
                        sb.AppendLine($"{indent}}}");
                    }
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
