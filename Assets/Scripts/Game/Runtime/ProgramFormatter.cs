using System.Collections.Generic;
using Cysharp.Text;

namespace Game.Runtime
{
    // 컴파일된 명령 목록을 코드 형태의 문자열로 변환한다 (디버그 로그·표시용).
    //   START
    //   태양광 패널의 방향(동쪽)
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
        private const string InfiniteCount = "무한";
        private const string NullValue = "null";

        /// <summary>
        /// 명령 목록을 START…END 코드 문자열로 만든다.
        /// </summary>
        public static string ToCode(IReadOnlyList<BlockInstruction> program, bool includeEnd)
        {
            // includeEnd: 체인이 완성하기(End)까지 도달했을 때만 END를 출력(미연결이면 생략).
            // 하위 메서드에 ref로 넘기므로 using 대신 finally에서 직접 반환한다
            Utf16ValueStringBuilder sb = ZString.CreateStringBuilder();
            try
            {
                AppendProgram(ref sb, program, includeEnd);
                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
        }

        /// <summary>
        /// 시작 표시 → 명령 → 끝 표시 → 함수 정의 순서로 코드를 쓴다.
        /// </summary>
        private static void AppendProgram(ref Utf16ValueStringBuilder sb, IReadOnlyList<BlockInstruction> program, bool includeEnd)
        {
            sb.AppendLine(StartMarker);

            List<FunctionInstruction> functions = new List<FunctionInstruction>();

            if (program is not null)
            {
                foreach (BlockInstruction instr in program)
                {
                    Append(ref sb, instr, 1);
                    CollectFunctions(instr, functions);
                }
            }

            sb.Append(includeEnd ? EndMarker : UnreachedEndMarker);

            // 같은 함수를 여러 번 호출해도 정의는 한 번만 출력한다
            HashSet<string> printedDefs = new HashSet<string>();
            foreach (FunctionInstruction fn in functions)
            {
                string defName = fn.DefName ?? Constants.CategoryNames.FunctionDef;
                if (!printedDefs.Add(defName)) continue;

                sb.AppendLine();
                sb.AppendLine();
                sb.Append(defName);
                sb.AppendLine(" {");
                AppendBody(ref sb, fn.Body, 1);
                sb.Append('}');
            }
        }

        /// <summary>
        /// 반복/만약 안에 들어간 함수 호출까지 수집한다.
        /// </summary>
        private static void CollectFunctions(BlockInstruction instr, List<FunctionInstruction> functions)
        {
            // 함수 본문 안의 함수는 수집하지 않는다 — 중첩 함수 정의는 지원 대상이 아니다.
            if (instr is FunctionInstruction fn)
            {
                functions.Add(fn);
                return;
            }

            foreach (List<BlockInstruction> body in InstructionTree.ChildBodies(instr))
                foreach (BlockInstruction child in body)
                    CollectFunctions(child, functions);
        }

        /// <summary>
        /// 명령 하나를 들여쓰기에 맞춰 코드 줄로 추가한다 (본문이 있으면 재귀).
        /// </summary>
        private static void Append(ref Utf16ValueStringBuilder sb, BlockInstruction instr, int depth)
        {
            int indent = depth * IndentSize;
            switch (instr)
            {
                case CommandInstruction cmd:
                    // 값 슬롯이 있는 명령은 이름(값) — 값 미연결이면 (null), 값 슬롯 없는 명령은 이름만
                    sb.Append(' ', indent);
                    sb.Append(cmd.Command);
                    if (cmd.Source && cmd.Source.ValueKind != ValueKind.None)
                    {
                        sb.Append('(');
                        sb.Append(cmd.Value ?? NullValue);
                        sb.Append(')');
                    }
                    sb.AppendLine();
                    break;

                case ActionInstruction act:
                    AppendLine(ref sb, indent, act.Action);
                    break;

                case ConditionActionInstruction cond:
                    AppendLine(ref sb, indent, cond.Action);
                    break;

                case FunctionInstruction fn:
                    AppendLine(ref sb, indent, fn.Name ?? Constants.CategoryNames.Function);
                    break;

                case RepeatInstruction rep:
                    // 블록 라벨에 '(무한)'이 이미 붙어 있어 블록 이름 대신 기본 이름에 횟수를 붙인다
                    sb.Append(' ', indent);
                    sb.Append(Constants.BlockLabels.While);
                    sb.Append('(');
                    if (rep.IsInfinite) sb.Append(InfiniteCount);
                    else sb.Append(rep.Count);
                    sb.AppendLine(") {");
                    AppendBody(ref sb, rep.Body, depth + 1);
                    AppendLine(ref sb, indent, "}");
                    break;

                case IfInstruction ifInstr:
                    sb.Append(' ', indent);
                    sb.Append(ifInstr.Source ? ifInstr.Source.name : Constants.BlockLabels.If);
                    sb.Append('(');
                    AppendCondition(ref sb, ifInstr.Condition);
                    sb.AppendLine(") {");
                    AppendBody(ref sb, ifInstr.Then, depth + 1);
                    // '아니면' 블록이 실제로 놓였을 때만 출력 — else가 비어 있어도(뒤에 블록이 없어도) 마커 자체는 표시
                    if (ifInstr.HasElseMarker)
                    {
                        int innerIndent = (depth + 1) * IndentSize;
                        AppendElseOpen(ref sb, innerIndent);
                        AppendBody(ref sb, ifInstr.Else, depth + 2);
                        AppendLine(ref sb, innerIndent, "}");
                    }
                    AppendLine(ref sb, indent, "}");
                    break;

                case ElseInstruction:
                    // '만약' 밖에 잘못 놓인 '아니면' — 컴파일은 실패하지만 실패 시 표시되는 코드에는 제자리에 나타난다
                    AppendElseOpen(ref sb, indent);
                    AppendLine(ref sb, indent, "}");
                    break;
            }
        }

        /// <summary>
        /// 들여쓰기 뒤에 텍스트를 쓰고 줄을 바꾼다.
        /// </summary>
        private static void AppendLine(ref Utf16ValueStringBuilder sb, int indent, string text)
        {
            sb.Append(' ', indent);
            sb.AppendLine(text);
        }

        /// <summary>
        /// '아니면 {' 줄을 쓴다.
        /// </summary>
        private static void AppendElseOpen(ref Utf16ValueStringBuilder sb, int indent)
        {
            sb.Append(' ', indent);
            sb.Append(Constants.BlockLabels.Else);
            sb.AppendLine(" {");
        }

        /// <summary>
        /// 본문의 명령들을 지정 깊이로 추가한다.
        /// </summary>
        private static void AppendBody(ref Utf16ValueStringBuilder sb, IReadOnlyList<BlockInstruction> body, int depth)
        {
            if (body is null) return;
            foreach (BlockInstruction instr in body)
                Append(ref sb, instr, depth);
        }

        /// <summary>
        /// 조건식을 코드 표기로 쓴다.
        /// </summary>
        private static void AppendCondition(ref Utf16ValueStringBuilder sb, ConditionExpr cond)
        {
            switch (cond)
            {
                case SimpleConditionExpr s:
                    sb.Append(s.Name);
                    break;
                case LogicConditionExpr l:
                    sb.Append(l.Left?.Name);
                    sb.Append(' ');
                    sb.Append(l.Operator);
                    sb.Append(' ');
                    sb.Append(l.Right?.Name ?? NullValue);
                    break;
                default:
                    sb.Append(NullValue);
                    break;
            }
        }
    }
}
