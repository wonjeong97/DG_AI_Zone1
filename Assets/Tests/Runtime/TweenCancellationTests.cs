using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Scenes;
using UnityEngine;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// DOTween 대기를 TweenCancelBehaviour.KillAndCancelAwait로 통일한 뒤의 타이밍·취소 동작을 검증한다.
    /// 이전 기본값(Kill)은 취소돼도 await가 정상 완료돼, 호출부가 취소된 줄 모르고 다음 연출을 이어 갔다.
    /// </summary>
    public class TweenCancellationTests
    {
        private float _originalTimeScale;
        private GameObject _go;

        /// <summary>
        /// 전역 timeScale을 기록하고 테스트용 오브젝트를 만든다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
            _go = new GameObject("TweenCancellationTests");
        }

        /// <summary>
        /// timeScale을 되돌리고 테스트용 오브젝트를 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _originalTimeScale;
            if (_go) UnityEngine.Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// 패널 페이드는 시스템 연출이라 timeScale이 0이어도 끝까지 진행된다.
        /// </summary>
        [UnityTest]
        public IEnumerator timeScale이_0이어도_패널_페이드가_완료된다() => UniTask.ToCoroutine(async () =>
        {
            CanvasGroup group = _go.AddComponent<CanvasGroup>();
            Time.timeScale = 0f;

            await SceneFader.FadeCanvasGroupAsync(group, 0f, 1f, 0.1f).AwaitWithRealtimeTimeout();

            Assert.AreEqual(1f, group.alpha, 0.001f, "timeScale=0에서 패널 페이드가 끝나지 않음");
        });

        /// <summary>
        /// 패널 페이드 도중 취소하면 OperationCanceledException으로 빠져나와 이후 연출이 이어지지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 패널_페이드를_취소하면_예외로_중단된다() => UniTask.ToCoroutine(async () =>
        {
            CanvasGroup group = _go.AddComponent<CanvasGroup>();
            using CancellationTokenSource cts = new CancellationTokenSource();

            UniTask fade = SceneFader.FadeCanvasGroupAsync(group, 0f, 1f, 5f, cts.Token);
            await UniTask.Yield();
            cts.Cancel();

            bool canceled = false;
            try
            {
                await fade.AwaitWithRealtimeTimeout();
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Assert.IsTrue(canceled, "취소가 예외로 전파되지 않아 호출부가 다음 연출을 이어 감");
            Assert.Less(group.alpha, 1f, "취소 후에도 페이드가 끝까지 진행됨");
        });

        /// <summary>
        /// 풍차 회전 램프업 도중 취소하면 예외로 중단되어 호출부가 다음 연출을 이어 가지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 풍차_회전_연출을_취소하면_예외로_중단된다() => UniTask.ToCoroutine(async () =>
        {
            WindTurbineSpin spin = _go.AddComponent<WindTurbineSpin>();
            spin.RampDuration = 5f;
            spin.SetNeutral();
            using CancellationTokenSource cts = new CancellationTokenSource();

            UniTask ramp = spin.ApplyAsync(100, cts.Token);
            await UniTask.Yield();
            cts.Cancel();

            bool canceled = false;
            try
            {
                await ramp.AwaitWithRealtimeTimeout();
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Assert.IsTrue(canceled, "취소가 예외로 전파되지 않음");
        });
    }
}
