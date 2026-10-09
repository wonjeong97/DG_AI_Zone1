using System.Collections.Generic;

namespace Game.Runtime
{
    /// <summary>
    /// 명령 트리(반복/만약/함수의 중첩 본문) 순회 공용 유틸.
    /// 컴파일 검증·채점·코드 출력이 각자 같은 재귀를 따로 구현하던 것을 한곳으로 모았다.
    /// </summary>
    public static class InstructionTree
    {
        /// <summary>
        /// 이 명령이 품고 있는 하위 본문 목록을 선언 순서대로 반환한다.
        /// (반복 → Body / 만약 → Then, Else / 함수 → Body / 그 외 → 없음)
        /// </summary>
        public static IEnumerable<List<BlockInstruction>> ChildBodies(BlockInstruction instr)
        {
            switch (instr)
            {
                case RepeatInstruction rep:
                    if (rep.Body is not null) yield return rep.Body;
                    break;

                case IfInstruction ifInstr:
                    if (ifInstr.Then is not null) yield return ifInstr.Then;
                    if (ifInstr.Else is not null) yield return ifInstr.Else;
                    break;

                case FunctionInstruction fn:
                    if (fn.Body is not null) yield return fn.Body;
                    break;
            }
        }

        /// <summary>
        /// 전위 순회(자기 자신 → 하위 본문 순). 검사 루틴이 "가장 먼저 만나는 블록"을 반환하던
        /// 기존 재귀 구현과 방문 순서가 동일하다.
        /// </summary>
        public static IEnumerable<BlockInstruction> Traverse(IEnumerable<BlockInstruction> instructions)
        {
            if (instructions is null) yield break;

            foreach (BlockInstruction instr in instructions)
            {
                yield return instr;

                foreach (List<BlockInstruction> body in ChildBodies(instr))
                    foreach (BlockInstruction nested in Traverse(body))
                        yield return nested;
            }
        }
    }
}
