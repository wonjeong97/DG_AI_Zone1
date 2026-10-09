using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 코딩 완료 뒤 결과 씬으로 넘어가기 전의 실행 연출(BlockExecutor)이 프로그램 순서대로 진행되고,
    /// 끝까지 가면 한 번만 완료를 알리는지 검증한다 — 완료를 알리지 않으면 결과 씬으로 넘어가지 못한다.
    /// </summary>
    public class BlockExecutorTests
    {
        /// <summary>
        /// 명령을 순서대로 실행하고 끝까지 가면 완료를 한 번 알린다.
        /// </summary>
        [UnityTest]
        public IEnumerator 명령을_순서대로_실행하고_끝나면_완료를_한_번_알린다() => UniTask.ToCoroutine(async () =>
        {
            List<string> executed = new List<string>();
            int completed = 0;
            BlockExecutor executor = RecordingExecutor(executed);
            executor.OnComplete += () => completed++;

            await executor.RunAsync(new List<BlockInstruction> { Cmd("A"), Cmd("B") }, CancellationToken.None)
                .AwaitWithRealtimeTimeout();

            CollectionAssert.AreEqual(new[] { "A", "B" }, executed);
            Assert.AreEqual(1, completed);
        });

        /// <summary>
        /// 무한 반복은 본문을 한 번만 실행하고 뒤 명령으로 넘어간다 (실행은 연출이라 멈추지 않는다).
        /// </summary>
        [UnityTest]
        public IEnumerator 무한_반복은_본문을_한_번만_실행하고_넘어간다() => UniTask.ToCoroutine(async () =>
        {
            List<string> executed = new List<string>();
            BlockExecutor executor = RecordingExecutor(executed);
            List<BlockInstruction> program = new List<BlockInstruction>
            {
                new RepeatInstruction { Count = -1, Body = new List<BlockInstruction> { Cmd("반복 안") } },
                Cmd("반복 뒤")
            };

            await executor.RunAsync(program, CancellationToken.None).AwaitWithRealtimeTimeout();

            CollectionAssert.AreEqual(new[] { "반복 안", "반복 뒤" }, executed);
        });

        /// <summary>
        /// 조건 평가가 거짓이면 아니면 분기만 실행한다.
        /// </summary>
        [UnityTest]
        public IEnumerator 조건이_거짓이면_아니면_분기만_실행한다() => UniTask.ToCoroutine(async () =>
        {
            List<string> executed = new List<string>();
            BlockExecutor executor = RecordingExecutor(executed);
            executor.OnCondition = _ => false;
            List<BlockInstruction> program = new List<BlockInstruction>
            {
                new IfInstruction
                {
                    Then = new List<BlockInstruction> { Cmd("만약 안") },
                    Else = new List<BlockInstruction> { Cmd("아니면 안") },
                    HasElseMarker = true
                }
            };

            await executor.RunAsync(program, CancellationToken.None).AwaitWithRealtimeTimeout();

            CollectionAssert.AreEqual(new[] { "아니면 안" }, executed);
        });

        /// <summary>
        /// 명령 처리기가 false를 돌려주면 그 자리에서 멈추고 완료를 알리지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 처리기가_중단하면_멈추고_완료를_알리지_않는다() => UniTask.ToCoroutine(async () =>
        {
            List<string> executed = new List<string>();
            int completed = 0;
            BlockExecutor executor = new BlockExecutor
            {
                OnExecute = (instr, _) =>
                {
                    string command = ((CommandInstruction)instr).Command;
                    executed.Add(command);
                    return UniTask.FromResult(command != "멈춤");
                }
            };
            executor.OnComplete += () => completed++;

            await executor.RunAsync(new List<BlockInstruction> { Cmd("멈춤"), Cmd("실행 안 됨") }, CancellationToken.None)
                .AwaitWithRealtimeTimeout();

            CollectionAssert.AreEqual(new[] { "멈춤" }, executed);
            Assert.AreEqual(0, completed);
        });

        /// <summary>
        /// 실행 도중 취소되면 예외 없이 멈추고 완료를 알리지 않는다 (씬을 떠나거나 다시 코딩 완료를 누른 경우).
        /// </summary>
        [UnityTest]
        public IEnumerator 취소되면_조용히_멈추고_완료를_알리지_않는다() => UniTask.ToCoroutine(async () =>
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            int completed = 0;
            BlockExecutor executor = new BlockExecutor
            {
                OnExecute = async (_, ct) =>
                {
                    cts.Cancel();
                    await UniTask.Delay(1000, cancellationToken: ct);
                    return true;
                }
            };
            executor.OnComplete += () => completed++;

            await executor.RunAsync(new List<BlockInstruction> { Cmd("A"), Cmd("B") }, cts.Token).AwaitWithRealtimeTimeout();

            Assert.AreEqual(0, completed);
        });

        /// <summary>
        /// 실행한 명령 이름을 순서대로 기록하는 실행기를 만든다.
        /// </summary>
        private static BlockExecutor RecordingExecutor(List<string> executed) => new BlockExecutor
        {
            OnExecute = (instr, _) =>
            {
                executed.Add(((CommandInstruction)instr).Command);
                return UniTask.FromResult(true);
            }
        };

        /// <summary>
        /// 이름만 있는 명령 노드를 만든다.
        /// </summary>
        private static CommandInstruction Cmd(string name) => new CommandInstruction { Command = name };
    }
}
