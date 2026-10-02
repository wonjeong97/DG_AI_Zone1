using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Scenes
{
    // 결과 씬(레벨5) 연구소 — 건물 안 노란 조명의 밝기로 에너지 효율을 보여준다.
    // SetNeutral()로 꺼 둔 뒤 ApplyAsync(효율%)로 서서히 밝힌다 — 0%면 꺼진 채, 100%면 최대 밝기.
    // 태양광 SolarPanelModelPose(자세), 풍력 WindTurbineSpin(속도), 수력 DamGateFlow(수문 개방),
    // 발전소 PowerPlantPump(피스톤)와 같은 자리에 대응한다.
    public class LabLightGlow : MonoBehaviour
    {
        [Tooltip("건물 안에 둔 조명들 — 모두 같은 비율로 밝아진다")]
        [SerializeField] private Light[] lights;
        [SerializeField] private float maxIntensity = 10f; // 효율 100%일 때 조명 세기
        [SerializeField] private float rampDuration = 1.5f;

        // 4_Result.json의 labLightDuration으로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float RampDuration
        {
            get => rampDuration;
            set => rampDuration = value;
        }

        private const float MaxPercent = 100f;

        private float _level;

        /// <summary>
        /// 기본 상태(조명 꺼짐)로 되돌린다 — 연출 시작점.
        /// </summary>
        public void SetNeutral() => SetLevel(0f);

        /// <summary>
        /// 에너지 효율(%)에 비례해 조명을 서서히 밝힌다.
        /// </summary>
        public async UniTask ApplyAsync(int percent, CancellationToken ct)
        {
            float target = Mathf.Clamp01(percent / MaxPercent);
            await DOVirtual.Float(_level, target, rampDuration, SetLevel)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            SetLevel(target);
        }

        /// <summary>
        /// 밝기 비율(0~1)을 모든 조명에 반영한다 — 0이면 조명을 꺼 그림자 계산도 하지 않는다.
        /// </summary>
        private void SetLevel(float level)
        {
            _level = Mathf.Clamp01(level);
            if (lights == null) return;

            foreach (Light l in lights)
            {
                if (!l) continue;
                l.intensity = maxIntensity * _level;
                l.enabled = _level > 0f;
            }
        }
    }
}
