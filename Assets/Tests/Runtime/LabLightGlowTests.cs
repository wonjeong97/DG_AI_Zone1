using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Scenes;
using UnityEngine;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 레벨5 결과 연구소 조명이 효율(%)에 비례해 밝아지고, 0%·기본 상태에서는 꺼져 있는지 검증한다.
    /// </summary>
    public class LabLightGlowTests
    {
        private GameObject _go;
        private Light _light;
        private LabLightGlow _glow;

        /// <summary>
        /// 조명 1개를 가진 연구소 조명 컴포넌트를 만든다 (조명 배열은 인스펙터 전용 필드라 리플렉션으로 넣는다).
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LabLightGlowTests");
            _light = _go.AddComponent<Light>();
            _glow = _go.AddComponent<LabLightGlow>();
            typeof(LabLightGlow).GetField("lights", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_glow, new[] { _light });
            _glow.RampDuration = 0.01f;
        }

        /// <summary>
        /// 테스트용 오브젝트를 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// 효율이 높을수록 밝고, 50%는 100%의 절반 밝기다. 기본 상태로 되돌리면 꺼진다.
        /// </summary>
        [UnityTest]
        public IEnumerator 효율에_비례해_밝아지고_기본_상태에서는_꺼진다() => UniTask.ToCoroutine(async () =>
        {
            await _glow.ApplyAsync(100, CancellationToken.None).AwaitWithRealtimeTimeout();
            float full = _light.intensity;
            Assert.IsTrue(_light.enabled, "100%인데 조명이 꺼져 있음");
            Assert.Greater(full, 0f);

            await _glow.ApplyAsync(50, CancellationToken.None).AwaitWithRealtimeTimeout();
            Assert.AreEqual(full * 0.5f, _light.intensity, 0.001f, "50%가 100%의 절반 밝기가 아님");

            _glow.SetNeutral();
            Assert.IsFalse(_light.enabled, "기본 상태인데 조명이 켜져 있음");
            Assert.AreEqual(0f, _light.intensity);
        });

        /// <summary>
        /// 효율 0%면 조명은 켜지지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 효율_0이면_조명이_꺼진_채로_있다() => UniTask.ToCoroutine(async () =>
        {
            _glow.SetNeutral();
            await _glow.ApplyAsync(0, CancellationToken.None).AwaitWithRealtimeTimeout();

            Assert.IsFalse(_light.enabled);
            Assert.AreEqual(0f, _light.intensity);
        });
    }
}
