using NUnit.Framework;
using Scenes;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 수력 결과 화면 물줄기의 흐름 위상 — 속도가 바뀌어도 튀지 않고, 오래 켜 두어도 셰이더 정밀도 안에 머무는지 검증한다.
    /// </summary>
    public class DamGateFlowTests
    {
        // DamWaterFlow.shader의 해시 주기(mod289)와 두 물결 층의 칸 이동 배수
        private const float ShaderHashPeriod = 289f;
        private const float SlowLayerMultiplier = 2.0f;
        private const float FastLayerMultiplier = 3.3f;

        /// <summary>
        /// 위상은 유속 × 경과 시간만큼 이어서 늘고, 주기를 넘으면 그 나머지에서 다시 시작한다.
        /// </summary>
        [Test]
        public void 위상은_주기를_넘으면_나머지에서_이어진다()
        {
            Assert.AreEqual(1.5f, DamGateFlow.AdvancePhase(1f, 0.5f, 1f), 1e-4f);

            float nearEnd = DamGateFlow.FlowPhasePeriod - 0.5f;
            Assert.AreEqual(0.5f, DamGateFlow.AdvancePhase(nearEnd, 1f, 1f), 1e-3f, "주기를 넘을 때 나머지에서 이어지지 않음");
        }

        /// <summary>
        /// 하루를 켜 두어도 위상은 주기 안에 머문다 — 셰이더 해시 입력이 커져 물결이 굳지 않게 한다.
        /// </summary>
        [Test]
        public void 하루를_켜_두어도_위상은_주기_안에_머문다()
        {
            const float secondsPerDay = 86400f;
            float phase = DamGateFlow.AdvancePhase(0f, 2f, secondsPerDay);

            Assert.GreaterOrEqual(phase, 0f);
            Assert.Less(phase, DamGateFlow.FlowPhasePeriod);
        }

        /// <summary>
        /// 위상 주기만큼 흐르면 두 물결 층 모두 셰이더 해시 주기의 정수배만큼 이동한다 — 주기를 되돌리는 순간에도 무늬가 끊기지 않는다.
        /// </summary>
        [Test]
        public void 위상_주기는_두_물결_층_모두_해시_주기의_정수배다()
        {
            float slowCells = DamGateFlow.FlowPhasePeriod * SlowLayerMultiplier / ShaderHashPeriod;
            float fastCells = DamGateFlow.FlowPhasePeriod * FastLayerMultiplier / ShaderHashPeriod;

            Assert.AreEqual(Mathf.Round(slowCells), slowCells, 1e-3f, "느린 층 이동이 해시 주기의 정수배가 아님");
            Assert.AreEqual(Mathf.Round(fastCells), fastCells, 1e-3f, "빠른 층 이동이 해시 주기의 정수배가 아님");
        }
    }
}
