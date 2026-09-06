using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Scenes
{
    // 결과 씬(레벨4) 발전소 — 발전량을 피스톤(Cylinder.008) 상하 운동과 수증기 양으로 보여준다.
    // SetNeutral()로 멈춰 세운 뒤 ApplyAsync(효율%)로 강도를 올린다 —
    // 태양광 SolarPanelModelPose(자세), 풍력 WindTurbineSpin(속도), 수력 DamGateFlow(수문 개방)와
    // 같은 자리에 대응한다.
    //
    // 다른 스테이지와 달리 효율에 그대로 비례하지 않는다. '부족' 구간에서는 아예 멈춰 있어야 하고
    // '보통'에서도 움직임이 보여야 해서, 부족/보통 경계 아래는 0, 그 위는 MinActiveIntensity에서
    // 시작하는 구간으로 다시 매핑한다.
    public class PowerPlantPump : MonoBehaviour
    {
        [SerializeField] private Transform piston;         // Cylinder.008 — 위아래로 움직이는 축
        [SerializeField] private ParticleSystem steam;     // 꼭대기에서 뿜는 흰 수증기
        [SerializeField] private float amplitude = 0.012f; // 강도 1일 때 상하 진폭 (piston의 로컬 단위)
        [SerializeField] private float minCycleSpeed = 2f; // 강도 하한일 때 왕복 각속도(rad/s)
        [SerializeField] private float maxCycleSpeed = 7f; // 강도 1일 때
        [SerializeField] private float maxSteamRate = 25f; // 강도 1일 때 초당 파티클 수
        [SerializeField] private float rampDuration = 1.5f;

        // 4_Result.json의 plantPumpDuration으로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float RampDuration
        {
            get => rampDuration;
            set => rampDuration = value;
        }

        // '보통' 구간 초입에서도 정지 상태와 구분되도록 주는 강도 하한
        private const float MinActiveIntensity = 0.35f;
        private const float MaxPercent = 100f;

        private Vector3 _pistonRest;
        private bool _restCaptured;
        private float _intensity;
        private float _phase;

        private void Awake() => CaptureRest();

        // Awake 전에 SetNeutral이 불릴 수 있어(스테이지를 켜는 순서에 따라) 안전하게 한 번만 기록한다
        private void CaptureRest()
        {
            if (_restCaptured || !piston) return;
            _pistonRest = piston.localPosition;
            _restCaptured = true;
        }

        private void Update()
        {
            if (!piston) return;

            // 강도가 0이면 위상이 멈추고 오프셋도 0이라 원래 자리에 그대로 선다
            if (_intensity > 0f)
                _phase += Mathf.Lerp(minCycleSpeed, maxCycleSpeed, _intensity) * Time.deltaTime;

            piston.localPosition = _pistonRest + Vector3.up * (amplitude * _intensity * Mathf.Sin(_phase));
        }

        // 기본 상태(정지, 수증기 없음) — 연출 시작점.
        public void SetNeutral()
        {
            CaptureRest();
            _phase = 0f;
            SetIntensity(0f);
            // 방출을 멈춰도 이미 떠 있는 입자는 수명이 끝날 때까지 남는다 — 씬 진입 순간
            // 이전 연출의 수증기가 비치지 않도록 지운다.
            if (steam) steam.Clear();
            if (piston) piston.localPosition = _pistonRest;
        }

        // 에너지 효율(%)에 맞춰 강도를 올린다. 부족 구간이면 0 — 피스톤도 수증기도 멈춘 채로 둔다.
        public async UniTask ApplyAsync(int percent, CancellationToken ct)
        {
            CaptureRest();
            float target = ToIntensity(percent);
            await DOVirtual.Float(_intensity, target, rampDuration, SetIntensity)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: ct);
            SetIntensity(target);
        }

        // 효율(%) → 연출 강도. 부족/보통 경계는 결과 텍스트와 같은 기준을 쓴다.
        private static float ToIntensity(int percent)
        {
            float poorCut = Constants.ResultMessages.NormalThresholdPercent;
            if (percent < poorCut) return 0f;
            return Mathf.Lerp(MinActiveIntensity, 1f, Mathf.InverseLerp(poorCut, MaxPercent, percent));
        }

        private void SetIntensity(float intensity)
        {
            _intensity = Mathf.Clamp01(intensity);
            if (!steam) return;
            ParticleSystem.EmissionModule em = steam.emission;
            em.rateOverTime = maxSteamRate * _intensity;
        }
    }
}
