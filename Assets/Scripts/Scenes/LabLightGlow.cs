using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Scenes
{
    // 결과 씬(레벨5) 연구소 — 건물 안 노란 조명으로 전력 수급 상태를 보여준다.
    // 결과 텍스트와 같은 경계로 나눈다 — 부족: 꺼짐 / 보통: 약하게 켜져 불안정하게 깜빡임 / 양호: 강하게 켜짐.
    // SetNeutral()로 꺼 둔 뒤 ApplyAsync(효율%)로 그 단계 밝기까지 서서히 올린다 —
    // 태양광 SolarPanelModelPose(자세), 풍력 WindTurbineSpin(속도), 수력 DamGateFlow(수문 개방),
    // 발전소 PowerPlantPump(피스톤)와 같은 자리에 대응한다.
    public class LabLightGlow : MonoBehaviour
    {
        [Tooltip("건물 안에 둔 조명들 — 모두 같은 세기로 켜진다. 비워 두면 자식 오브젝트의 조명을 모두 쓴다")]
        [SerializeField] private Light[] lights;

        // 창문 너머 실내 벽이 조명과 가까워 낮은 세기에서도 금방 밝아진다 — 보통은 창문 몇 개만 흐리게 보이는
        // 아주 낮은 값, 양호는 타워 위아래 창문까지 전부 밝게 보이는 값으로 둔다 (결과 화면 240x160 기준)
        [SerializeField] private float weakIntensity = 0.16f; // 보통일 때 조명 세기
        [SerializeField] private float strongIntensity = 8f;  // 양호일 때 조명 세기
        [SerializeField] private float rampDuration = 1.5f;

        [Header("보통 — 불안정한 깜빡임")]
        [SerializeField] private float flickerSpeed = 9f;   // 밝기가 흔들리는 속도(노이즈 진행 속도)
        [Range(0f, 1f)]
        [SerializeField] private float flickerDepth = 0.6f; // 흔들릴 때 가장 어두워지는 비율
        [SerializeField] private Vector2 dropoutInterval = new(0.4f, 1.6f);  // 순간적으로 꺼지는 간격(초, 최소~최대)
        [SerializeField] private Vector2 dropoutDuration = new(0.04f, 0.14f); // 꺼져 있는 시간(초, 최소~최대)

        // 4_Result.json의 labLightDuration으로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float RampDuration
        {
            get => rampDuration;
            set => rampDuration = value;
        }

        private float _level;         // 단계 조명 세기 — 트윈이 움직이는 값
        private bool _flickering;     // 보통 단계에 도달해 깜빡이는 중
        private float _noiseSeed;
        private float _nextDropoutAt;
        private float _dropoutEndAt;

        /// <summary>
        /// 보통 단계에서 밝기를 흔들고, 불규칙한 간격으로 잠깐씩 끈다.
        /// </summary>
        private void Update()
        {
            if (!_flickering) return;

            float now = Time.time;
            if (now >= _nextDropoutAt)
            {
                _dropoutEndAt = now + Random.Range(dropoutDuration.x, dropoutDuration.y);
                _nextDropoutAt = _dropoutEndAt + Random.Range(dropoutInterval.x, dropoutInterval.y);
            }

            // PerlinNoise는 0~1을 살짝 벗어날 수 있어 잘라 낸다 — 보통 밝기를 넘지 않게
            float factor = now < _dropoutEndAt
                ? 0f
                : 1f - flickerDepth * Mathf.Clamp01(Mathf.PerlinNoise(now * flickerSpeed, _noiseSeed));
            ApplyIntensity(_level * factor);
        }

        /// <summary>
        /// 기본 상태(조명 꺼짐, 깜빡임 없음)로 되돌린다 — 연출 시작점.
        /// </summary>
        public void SetNeutral()
        {
            _flickering = false;
            SetLevel(0f);
        }

        /// <summary>
        /// 에너지 효율(%)이 속한 단계의 세기까지 서서히 올린다 — 보통이면 도달한 뒤부터 깜빡인다.
        /// </summary>
        public async UniTask ApplyAsync(int percent, CancellationToken ct)
        {
            _flickering = false;
            float target = ToIntensity(percent);
            await DOVirtual.Float(_level, target, rampDuration, SetLevel)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            SetLevel(target);

            if (IsNormal(percent)) StartFlicker();
        }

        /// <summary>
        /// 효율(%)을 단계 조명 세기로 바꾼다 — 부족 0 / 보통 weakIntensity / 양호 strongIntensity. 경계는 결과 텍스트와 같은 기준을 쓴다.
        /// </summary>
        private float ToIntensity(int percent)
        {
            if (percent < Constants.ResultMessages.NormalThresholdPercent) return 0f;
            return IsNormal(percent) ? weakIntensity : strongIntensity;
        }

        /// <summary>
        /// 효율(%)이 보통 구간인지 확인한다.
        /// </summary>
        private static bool IsNormal(int percent) =>
            percent >= Constants.ResultMessages.NormalThresholdPercent &&
            percent < Constants.ResultMessages.GoodThresholdPercent;

        /// <summary>
        /// 깜빡임을 시작한다 — 첫 순간 꺼짐은 바로 오지 않도록 간격 하나만큼 미룬다.
        /// </summary>
        private void StartFlicker()
        {
            _noiseSeed = Random.value * 100f;
            _dropoutEndAt = 0f;
            _nextDropoutAt = Time.time + Random.Range(dropoutInterval.x, dropoutInterval.y);
            _flickering = true;
        }

        /// <summary>
        /// 단계 조명 세기를 기록하고 조명에 반영한다.
        /// </summary>
        private void SetLevel(float intensity)
        {
            _level = Mathf.Max(0f, intensity);
            ApplyIntensity(_level);
        }

        /// <summary>
        /// 조명 세기를 모든 조명에 반영한다 — 조명 배열이 비어 있으면 자식 조명을 모아 쓰고, 0이면 조명을 꺼 그림자 계산도 하지 않는다.
        /// </summary>
        private void ApplyIntensity(float intensity)
        {
            if (lights == null || lights.Length == 0)
                lights = GetComponentsInChildren<Light>(true);

            foreach (Light l in lights)
            {
                if (!l) continue;
                l.intensity = intensity;
                l.enabled = intensity > 0f;
            }
        }
    }
}
