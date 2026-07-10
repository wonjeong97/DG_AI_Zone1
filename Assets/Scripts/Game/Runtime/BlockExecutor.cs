using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DG.Game.Runtime
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

        public async UniTask RunAsync(List<BlockInstruction> program, CancellationToken ct)
        {
            try
            {
                if (await ExecuteList(program, ct))
                    OnComplete?.Invoke();
            }
            catch (OperationCanceledException) { }
        }

        private async UniTask<bool> ExecuteList(List<BlockInstruction> list, CancellationToken ct)
        {
            foreach (var instr in list)
            {
                ct.ThrowIfCancellationRequested();
                if (!await ExecuteOne(instr, ct)) return false;
            }
            return true;
        }

        private async UniTask<bool> ExecuteOne(BlockInstruction instr, CancellationToken ct)
        {
            OnBlockEnter?.Invoke(instr.Source);

            return instr switch
            {
                IfInstruction     i => await ExecuteIf(i, ct),
                RepeatInstruction r => await ExecuteRepeat(r, ct),
                _                   => OnExecute is not null ? await OnExecute(instr, ct) : true
            };
        }

        private async UniTask<bool> ExecuteIf(IfInstruction instr, CancellationToken ct)
        {
            bool cond   = OnCondition?.Invoke(instr.Condition) ?? false;
            var  branch = cond ? instr.Then : instr.Else;
            if (branch is null || branch.Count == 0) return true;
            return await ExecuteList(branch, ct);
        }

        private async UniTask<bool> ExecuteRepeat(RepeatInstruction instr, CancellationToken ct)
        {
            for (int i = 0; i < instr.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (!await ExecuteList(instr.Body, ct)) return false;
            }
            return true;
        }
    }
}
