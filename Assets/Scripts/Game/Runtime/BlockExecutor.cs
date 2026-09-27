using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Runtime
{
    public class BlockExecutor
    {
        // Command / Action / ConditionAction 등 리프 명령 처리기.
        // false 반환 시 실행 중단.
        public Func<BlockInstruction, CancellationToken, UniTask<bool>> OnExecute;

        // If 조건 평가기. true = Then 분기, false = Else 분기.
        public Func<ConditionExpr, bool> OnCondition;

        // 각 명령이 시작될 때 호출 (시각적 하이라이트 등에 활용).
        public Action<CodingBlock> OnBlockEnter;

        public event Action OnComplete;

        /// <summary>
        /// 프로그램을 순서대로 실행하고, 끝까지 실행되면 OnComplete를 알린다 (취소 시 조용히 중단).
        /// </summary>
        public async UniTask RunAsync(List<BlockInstruction> program, CancellationToken ct)
        {
            try
            {
                if (await ExecuteList(program, ct))
                    OnComplete?.Invoke();
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 명령 목록을 순서대로 실행한다 (하나라도 false면 중단).
        /// </summary>
        private async UniTask<bool> ExecuteList(List<BlockInstruction> list, CancellationToken ct)
        {
            foreach (BlockInstruction instr in list)
            {
                ct.ThrowIfCancellationRequested();
                if (!await ExecuteOne(instr, ct)) return false;
            }
            return true;
        }

        /// <summary>
        /// 명령 하나를 종류에 맞게 실행한다 (리프 명령은 OnExecute에 위임).
        /// </summary>
        private async UniTask<bool> ExecuteOne(BlockInstruction instr, CancellationToken ct)
        {
            OnBlockEnter?.Invoke(instr.Source);

            return instr switch
            {
                IfInstruction       i => await ExecuteIf(i, ct),
                RepeatInstruction   r => await ExecuteRepeat(r, ct),
                FunctionInstruction f => await ExecuteFunction(f, ct),
                _                     => OnExecute is not null ? await OnExecute(instr, ct) : true
            };
        }

        /// <summary>
        /// 함수 호출 명령의 본문을 실행한다.
        /// </summary>
        private async UniTask<bool> ExecuteFunction(FunctionInstruction instr, CancellationToken ct)
        {
            if (instr.Body is null || instr.Body.Count == 0) return true;
            return await ExecuteList(instr.Body, ct);
        }

        /// <summary>
        /// 조건 평가 결과에 따라 Then 또는 Else 분기를 실행한다.
        /// </summary>
        private async UniTask<bool> ExecuteIf(IfInstruction instr, CancellationToken ct)
        {
            bool cond   = OnCondition?.Invoke(instr.Condition) ?? false;
            List<BlockInstruction> branch = cond ? instr.Then : instr.Else;
            if (branch is null || branch.Count == 0) return true;
            return await ExecuteList(branch, ct);
        }

        /// <summary>
        /// 반복 명령의 본문을 지정 횟수만큼 실행한다 (무한 반복은 1회로 제한).
        /// </summary>
        private async UniTask<bool> ExecuteRepeat(RepeatInstruction instr, CancellationToken ct)
        {
            // 무한 반복은 이론적 의미만 가짐 — 실제 실행은 1회로 제한 (무한 루프 방지)
            int iterations = instr.IsInfinite ? 1 : instr.Count;
            for (int i = 0; i < iterations; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (!await ExecuteList(instr.Body, ct)) return false;
            }
            return true;
        }
    }
}
