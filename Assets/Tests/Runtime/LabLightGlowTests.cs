using System.Collections;
using System.Collections.Generic;
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
    /// 레벨5 결과 연구소 조명이 전력 수급 단계(부족 꺼짐·보통 약하게 깜빡임·양호 강하게)대로 켜지는지 검증한다.
    /// </summary>
    public class LabLightGlowTests
    {
        private const int FlickerSampleFrames = 30;

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
        /// 양호(75% 이상)는 75%든 100%든 같은 강한 밝기로 켜지고, 기본 상태로 되돌리면 꺼진다.
        /// </summary>
        [UnityTest]
        public IEnumerator 양호는_강하게_켜지고_기본_상태에서는_꺼진다() => UniTask.ToCoroutine(async () =>
        {
            await _glow.ApplyAsync(100, CancellationToken.None).AwaitWithRealtimeTimeout();
            float strong = _light.intensity;
            Assert.IsTrue(_light.enabled, "양호인데 조명이 꺼져 있음");
            Assert.Greater(strong, 0f);

            await _glow.ApplyAsync(75, CancellationToken.None).AwaitWithRealtimeTimeout();
            await UniTask.Yield();
            Assert.AreEqual(strong, _light.intensity, 0.001f, "75%(양호)가 100%와 다른 밝기");

            _glow.SetNeutral();
            Assert.IsFalse(_light.enabled, "기본 상태인데 조명이 켜져 있음");
            Assert.AreEqual(0f, _light.intensity);
        });

        /// <summary>
        /// 부족(50% 미만)이면 조명은 켜지지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 부족이면_조명이_꺼진_채로_있다() => UniTask.ToCoroutine(async () =>
        {
            _glow.SetNeutral();
            await _glow.ApplyAsync(25, CancellationToken.None).AwaitWithRealtimeTimeout();
            await UniTask.Yield();

            Assert.IsFalse(_light.enabled);
            Assert.AreEqual(0f, _light.intensity);
        });

        /// <summary>
        /// 보통(50% 이상 75% 미만)은 양호보다 약하게 켜지고, 프레임마다 밝기가 흔들리되 약한 밝기를 넘지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 보통은_약하게_켜져_불안정하게_깜빡인다() => UniTask.ToCoroutine(async () =>
        {
            await _glow.ApplyAsync(100, CancellationToken.None).AwaitWithRealtimeTimeout();
            float strong = _light.intensity;

            await _glow.ApplyAsync(50, CancellationToken.None).AwaitWithRealtimeTimeout();
            float weak = _light.intensity;
            Assert.Greater(weak, 0f, "보통인데 조명이 꺼져 있음");
            Assert.Less(weak, strong, "보통이 양호만큼 밝음");

            HashSet<float> samples = new HashSet<float>();
            for (int i = 0; i < FlickerSampleFrames; i++)
            {
                await UniTask.Yield();
                Assert.LessOrEqual(_light.intensity, weak + 0.001f, "깜빡임이 보통 밝기를 넘어섬");
                samples.Add(_light.intensity);
            }
            Assert.Greater(samples.Count, 1, "보통인데 밝기가 흔들리지 않음");

            _glow.SetNeutral();
            await UniTask.Yield();
            Assert.IsFalse(_light.enabled, "기본 상태로 되돌린 뒤에도 깜빡임이 남아 조명이 켜짐");
        });
    }
}
