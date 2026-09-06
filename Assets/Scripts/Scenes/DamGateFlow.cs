using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Scenes
{
    // 댐 수문 1개와 그 수문 자리의 물줄기를 묶어 제어한다.
    // 수문은 피벗이 상단이라 Z스케일을 줄이면 아래에서부터 열린다.
    // 개방량에 비례해 물의 양(폭·투명도·유속)이 함께 변한다.
    //
    // 주의: FBX 임포트 스케일 때문에 게이트의 '닫힘' 스케일은 1이 아니다(예: 41.84).
    // 그래서 절대값을 대입하지 않고, 최초 1회 기록한 기준 스케일에 비율을 곱한다.
    // 물줄기 폭도 Transform 스케일이 아니라 셰이더(_WidthFrac)에서 처리한다.
    [System.Serializable]
    public class DamGate
    {
        public Transform gate;                 // Gate_1 ~ Gate_4
        public Transform water;                // Water_Bay_1 ~ Water_Bay_4
        public ParticleSystem foam;            // Foam_Bay_1 ~ Foam_Bay_4 (착수 물보라)
        public Renderer foamPool;              // FoamPool_Bay_1 ~ 4 (착수 포말 웅덩이)
        [Range(0f, 1f)] public float opening = 1f;   // 0=닫힘, 1=완전개방

        [Tooltip("닫혔을 때 게이트의 Z스케일. 0이면 최초 실행 시 자동 기록.")]
        public float closedScaleZ = 0f;
    }

    // 결과 씬(레벨3)에서는 수문 개방량이 발전 결과를 보여주는 연출이다.
    // SetNeutral()로 전부 닫아 둔 뒤 ApplyAsync(효율%)로 서서히 연다 —
    // 태양광의 SolarPanelModelPose(자세), 풍력의 WindTurbineSpin(속도)과 같은 자리에 대응한다.
    [ExecuteAlways]
    public class DamGateFlow : MonoBehaviour
    {
        [SerializeField] private DamGate[] gates = new DamGate[4];
        [SerializeField] private float openDuration = 3f; // 닫힘 → 목표 개방량까지 걸리는 시간(초)

        // 4_Result.json의 damOpenDuration으로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float OpenDuration
        {
            get => openDuration;
            set => openDuration = value;
        }

        [Header("물")]
        [Range(0f, 1f)]
        [Tooltip("1이면 개방량과 무관하게 항상 수로 폭을 꽉 채운다.")]
        [SerializeField] private float minStreamWidth = 1f;      // 살짝 열렸을 때 물줄기 폭 비율
        [Range(0f, 1f)]
        [Tooltip("1이면 물이 항상 마루에서 시작한다. 낮추면 아래쪽만 흘러 '중간에서 시작'하는 느낌이 나므로 보통 1 권장.")]
        [SerializeField] private float minStreamHeight = 1f;     // 살짝 열렸을 때 물기둥 높이 비율
        [Range(0f, 1f)]
        [SerializeField] private float minStreamThickness = 0.2f; // 살짝 열렸을 때 물 두께 비율(블렌드셰이프)
        [SerializeField] private float minFlowSpeed = 0.4f;
        [SerializeField] private float maxFlowSpeed = 2.0f;

        [Header("착수 거품")]
        [SerializeField] private float minFoamRate = 20f;
        [SerializeField] private float maxFoamRate = 130f;

        private static readonly int OpeningId = Shader.PropertyToID("_Opening");
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");
        private static readonly int WidthId = Shader.PropertyToID("_WidthFrac");
        private static readonly int HeightId = Shader.PropertyToID("_HeightFrac");
        private static readonly int FlowFracId = Shader.PropertyToID("_FlowFrac");

        private const float MaxPercent = 100f;
        private const float ClosedEpsilon = 0.001f;

        // 물줄기가 마루를 넘기 시작/토우에 도달하는 시점 (전체 연출 시간 대비).
        // 수문이 살짝 열린 직후 흐르기 시작해, 수문이 다 열리기 한참 전에 바닥까지 닿는다 —
        // 끝까지 늘리면 물이 슬로모션처럼 천천히 내려가 오히려 부자연스럽다.
        private const float StreamStartT = 0.15f;
        private const float StreamEndT = 0.6f;

        private MaterialPropertyBlock _mpb;
        private float _currentOpening;

        // 물줄기가 마루에서 토우까지 내려간 정도(0=아직 안 흐름, 1=토우 도달).
        // 연출을 쓰지 않는 씬에서는 기존처럼 항상 끝까지 흐르도록 1에서 시작한다.
        private float _flowReach = 1f;

        private void OnEnable() => ApplyAll();
        private void Update() => ApplyAll();
        private void OnValidate() => ApplyAll();

        // 기본 상태(전부 닫힘, 물 없음) — 연출 시작점.
        public void SetNeutral()
        {
            SetOpeningValue(0f);
            _flowReach = 0f;
            ApplyAll();
        }

        // 에너지 효율(%)에 비례해 수문을 연다 — 0%면 닫힌 채, 100%면 완전 개방.
        // 수문 4개를 같은 양만큼 여닫는다(레벨3은 수문별 값을 따로 받지 않는다).
        //
        // 수문과 물줄기는 서로 다른 곡선으로 움직인다. 수문은 전 구간에 걸쳐 천천히 열리고,
        // 물은 수문이 조금 열린 뒤에야 마루를 넘어 아래로 내려간다 —
        // 둘을 같이 움직이면 살짝 열린 순간 물이 이미 토우까지 닿아 있어 뚝 끊기듯 보인다.
        // 도달 거리는 최종 개방량과 무관하게 항상 1(토우)까지 간다. 조금만 열려도 물은 끝까지 떨어진다.
        public async UniTask ApplyAsync(int percent, CancellationToken ct)
        {
            float targetOpening = Mathf.Clamp01(percent / MaxPercent);
            float targetReach = targetOpening > ClosedEpsilon ? 1f : 0f;
            float startOpening = _currentOpening;
            float startReach = _flowReach;

            await DOVirtual.Float(0f, 1f, openDuration, t =>
                {
                    SetOpeningValue(Mathf.Lerp(startOpening, targetOpening,
                        DOVirtual.EasedValue(0f, 1f, t, Ease.InOutSine)));

                    float streamT = Mathf.InverseLerp(StreamStartT, StreamEndT, t);
                    _flowReach = Mathf.Lerp(startReach, targetReach,
                        DOVirtual.EasedValue(0f, 1f, streamT, Ease.InQuad));

                    ApplyAll();
                })
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: ct);

            SetOpeningValue(targetOpening);
            _flowReach = targetReach;
            ApplyAll();
        }

        private void SetOpeningValue(float opening)
        {
            _currentOpening = opening;
            if (gates == null) return;
            foreach (DamGate g in gates)
            {
                if (g != null) g.opening = opening;
            }
        }

        public void ApplyAll()
        {
            if (gates == null) return;
            _mpb ??= new MaterialPropertyBlock();
            foreach (DamGate g in gates)
            {
                if (g != null) Apply(g);
            }
        }

        private void Apply(DamGate g)
        {
            float opening = Mathf.Clamp01(g.opening);

            // 수문 - 기준(닫힘) 스케일에 비율을 곱한다
            if (g.gate)
            {
                if (g.closedScaleZ <= 0f) g.closedScaleZ = g.gate.localScale.z;
                Vector3 s = g.gate.localScale;
                s.z = g.closedScaleZ * (1f - opening);
                g.gate.localScale = s;
            }

            if (!g.water) return;

            bool closed = opening <= ClosedEpsilon;

            // 착수 거품 - 많이 열릴수록 거세진다
            if (g.foam)
            {
                ParticleSystem.EmissionModule em = g.foam.emission;
                // 물이 아직 내려오는 중이면 착수 지점에 거품이 생길 리 없다 — 도달한 만큼만 낸다
                em.rateOverTime = closed ? 0f : Mathf.Lerp(minFoamRate, maxFoamRate, opening) * _flowReach;
                ParticleSystem.MainModule fm = g.foam.main;
                fm.startSpeed = new ParticleSystem.MinMaxCurve(
                    Mathf.Lerp(0.03f, 0.08f, opening), Mathf.Lerp(0.10f, 0.24f, opening));
            }

            // 착수 포말 웅덩이 - 개방량만큼 진해지고 빨라진다
            if (g.foamPool)
            {
                g.foamPool.GetPropertyBlock(_mpb);
                _mpb.SetFloat(OpeningId, closed ? 0f : Mathf.Lerp(0.45f, 1f, opening) * _flowReach);
                _mpb.SetFloat(SpeedId, Mathf.Lerp(0.5f, 1.4f, opening));
                g.foamPool.SetPropertyBlock(_mpb);
            }

            // 두께 - 블렌드셰이프 "Thick" (많이 열릴수록 물이 두꺼워진다)
            if (g.water.TryGetComponent(out SkinnedMeshRenderer smr) &&
                smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                float thick = closed ? 0f : Mathf.Lerp(minStreamThickness, 1f, opening);
                smr.SetBlendShapeWeight(0, thick * 100f);
            }

            // 물 - 폭/투명도/유속은 셰이더로 처리한다.
            // SetActive는 OnValidate 중 호출이 금지되어 있어, 닫히면 완전 투명으로 없앤다.
            if (g.water.TryGetComponent(out Renderer r))
            {
                r.GetPropertyBlock(_mpb);
                // 폭·투명도는 고정, 개방량에 따라 달라지는 건 두께(블렌드셰이프)와 유속뿐
                _mpb.SetFloat(WidthId, closed ? 0f : Mathf.Lerp(minStreamWidth, 1f, opening));
                _mpb.SetFloat(HeightId, closed ? 0f : Mathf.Lerp(minStreamHeight, 1f, opening));
                _mpb.SetFloat(FlowFracId, closed ? 0f : _flowReach);
                _mpb.SetFloat(OpeningId, closed ? 0f : 1f);
                _mpb.SetFloat(SpeedId, Mathf.Lerp(minFlowSpeed, maxFlowSpeed, opening));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
