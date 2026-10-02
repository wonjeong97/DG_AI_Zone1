using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuliacDev.Core;
using NUnit.Framework;
using Scenes;
using UnityEngine;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 결과 연출이 비활동 타이머를 멈췄다가 어떤 경로로 끝나도 다시 켜는지 검증한다.
    /// 이전에는 취소가 아닌 예외로 연출이 끊기면 타이머가 멈춘 채 남아, 체험자가 떠나도 타이틀로 돌아가지 않았다.
    /// </summary>
    public class ResultSequenceTimerTests
    {
        private GameObject _go;
        private InactivityTimer _timer;

        /// <summary>
        /// 비활성 오브젝트에 타이머를 붙여 싱글톤 처리와 주입 검사(Start) 없이 일시 정지 상태만 다룬다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ResultSequenceTimerTests");
            _go.SetActive(false);
            _timer = _go.AddComponent<InactivityTimer>();
        }

        /// <summary>
        /// 테스트용 오브젝트를 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_go) UnityEngine.Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// 연출이 진행되는 동안에는 비활동 타이머가 멈춰 있다.
        /// </summary>
        [UnityTest]
        public IEnumerator 연출이_진행되는_동안_비활동_타이머가_멈춘다() => UniTask.ToCoroutine(async () =>
        {
            bool pausedDuringSteps = false;

            await ResultSequence.RunWithTimerPausedAsync(_timer, _ =>
            {
                pausedDuringSteps = _timer.IsPaused;
                return UniTask.CompletedTask;
            }, CancellationToken.None).AwaitWithRealtimeTimeout();

            Assert.IsTrue(pausedDuringSteps, "연출 중에 비활동 타이머가 멈추지 않음");
        });

        /// <summary>
        /// 연출 도중 예외가 나도 비활동 타이머가 다시 켜지고 오류가 전달된다.
        /// </summary>
        [UnityTest]
        public IEnumerator 연출_중_예외가_나도_비활동_타이머가_다시_켜진다() => UniTask.ToCoroutine(async () =>
        {
            Exception reported = null;

            await ResultSequence.RunWithTimerPausedAsync(_timer,
                _ => throw new InvalidOperationException("연출 실패"),
                CancellationToken.None,
                ex => reported = ex).AwaitWithRealtimeTimeout();

            Assert.IsFalse(_timer.IsPaused, "예외로 끝난 뒤 비활동 타이머가 멈춘 채 남음");
            Assert.IsInstanceOf<InvalidOperationException>(reported, "연출 오류가 로그 콜백으로 전달되지 않음");
        });

        /// <summary>
        /// 씬 전환 등으로 연출이 취소돼도 비활동 타이머가 다시 켜지고, 취소는 오류로 보고되지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 연출이_취소돼도_비활동_타이머가_다시_켜진다() => UniTask.ToCoroutine(async () =>
        {
            Exception reported = null;

            await ResultSequence.RunWithTimerPausedAsync(_timer,
                _ => throw new OperationCanceledException(),
                CancellationToken.None,
                ex => reported = ex).AwaitWithRealtimeTimeout();

            Assert.IsFalse(_timer.IsPaused, "취소로 끝난 뒤 비활동 타이머가 멈춘 채 남음");
            Assert.IsNull(reported, "정상 취소가 오류로 보고됨");
        });
    }
}
