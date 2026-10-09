using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

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

        // 물줄기 렌더러 캐시 — 트윈 중 매 프레임 water에서 다시 찾지 않도록 water가 바뀔 때만 새로 찾는다
        [System.NonSerialized] public Transform cachedWater;
        [System.NonSerialized] public SkinnedMeshRenderer waterSkin;
        [System.NonSerialized] public Renderer waterRenderer;

        // 물줄기·포말 웅덩이 무늬의 흐름 위상 — 유속 × 프레임 시간으로 쌓는다
        [System.NonSerialized] public float waterPhase;
        [System.NonSerialized] public float foamPhase;
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
        [Tooltip("거품 파티클 시작 속도 범위(최소~최대) — 살짝 열렸을 때")]
        [SerializeField] private Vector2 minFoamStartSpeed = new(0.03f, 0.10f);
        [Tooltip("거품 파티클 시작 속도 범위(최소~최대) — 완전히 열렸을 때")]
        [SerializeField] private Vector2 maxFoamStartSpeed = new(0.08f, 0.24f);

        [Header("착수 포말 웅덩이")]
        [Tooltip("포말 진하기 — 살짝 열렸을 때 / 완전히 열렸을 때")]
        [SerializeField] private Vector2 foamPoolOpeningRange = new(0.45f, 1f);
        [Tooltip("포말 흐름 속도 — 살짝 열렸을 때 / 완전히 열렸을 때")]
        [SerializeField] private Vector2 foamPoolSpeedRange = new(0.5f, 1.4f);

        private readonly static int OpeningId = Shader.PropertyToID("_Opening");
        private readonly static int PhaseId = Shader.PropertyToID("_Phase");
        private readonly static int WidthId = Shader.PropertyToID("_WidthFrac");
        private readonly static int HeightId = Shader.PropertyToID("_HeightFrac");
        private readonly static int FlowFracId = Shader.PropertyToID("_FlowFrac");

        private const float MaxPercent = Constants.ResultMessages.MaxPercent;
        private const float ClosedEpsilon = 0.001f;

        // 흐름 위상을 되돌리는 주기 — 셰이더 해시 주기(289칸)와 두 물결 층의 칸 이동 배수(2.0·3.3)가 모두 정수 칸이 되는 값
        public const float FlowPhasePeriod = 2890f;

        // 물 메시의 블렌드셰이프 "Thick" 순번과 최대 가중치
        private const int ThickBlendShapeIndex = 0;
        private const float MaxBlendShapeWeight = 100f;

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

        /// <summary>
        /// 활성화될 때 현재 개방량을 수문·물줄기에 반영한다.
        /// </summary>
        private void OnEnable() => ApplyAll();

        private ILogger<DamGateFlow> _logger;

        /// <summary>
        /// 로거를 주입받는다 (결과 씬을 불러올 때 GameLifetimeScope가 주입).
        /// </summary>
        [Inject]
        public void Construct(ILogger<DamGateFlow> logger) => _logger = logger;

        /// <summary>
        /// 연출에 필요한 수문 참조가 빠져 있으면 경고한다 (매 프레임 건너뛰는 곳마다 남기면 로그가 넘치므로 시작할 때 한 번만, 주입이 없는 에디터 모드는 건너뛴다).
        /// </summary>
        private void Start()
        {
            if (_logger == null) return;
            if (gates == null || gates.Length == 0)
            {
                _logger.ZLogWarning($"[DamGateFlow] {name}에 gates가 비어 있어 수문·물줄기가 움직이지 않습니다.");
                return;
            }

            for (int i = 0; i < gates.Length; i++)
            {
                DamGate g = gates[i];
                if (g == null) continue;
                if (!g.gate) _logger.ZLogWarning($"[DamGateFlow] {name}의 {i + 1}번 수문에 gate가 할당되지 않아 수문이 열리지 않습니다.");
                if (!g.water) _logger.ZLogWarning($"[DamGateFlow] {name}의 {i + 1}번 수문에 water가 할당되지 않아 물줄기·거품이 나오지 않습니다.");
            }
        }

        /// <summary>
        /// 물 무늬의 흐름 위상을 매 프레임 쌓아 셰이더에 넘긴다 (에디터에서는 인스펙터로 바꾼 개방량도 함께 반영한다).
        /// </summary>
        private void Update()
        {
            // 빌드에서는 스케일·파티클·폭 같은 값은 바뀌는 시점(OnEnable/SetNeutral/ApplyAsync)에만 넣고 위상만 매 프레임 넣는다.
            // 에디터에서는 인스펙터로 opening을 직접 만지며 확인하는 용도라 플레이 중에도 전부 반영한다
#if UNITY_EDITOR
            ApplyAll();
#endif
            AdvanceFlowPhases(Time.deltaTime);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 인스펙터 값이 바뀌면 즉시 반영한다.
        /// </summary>
        private void OnValidate() => ApplyAll();
#endif

        /// <summary>
        /// 흐름 위상을 유속 × 경과 시간만큼 앞으로 보내고 주기 안으로 되돌린다.
        /// </summary>
        public static float AdvancePhase(float phase, float speed, float deltaTime) =>
            Mathf.Repeat(phase + speed * deltaTime, FlowPhasePeriod);

        /// <summary>
        /// 모든 수문의 물줄기·포말 웅덩이 위상을 개방량에 맞는 유속으로 앞으로 보내 셰이더에 넣는다.
        /// </summary>
        private void AdvanceFlowPhases(float deltaTime)
        {
            if (gates == null) return;
            _mpb ??= new MaterialPropertyBlock();
            foreach (DamGate g in gates)
            {
                if (g == null) continue;

                float opening = Mathf.Clamp01(g.opening);
                g.waterPhase = AdvancePhase(g.waterPhase, Mathf.Lerp(minFlowSpeed, maxFlowSpeed, opening), deltaTime);
                g.foamPhase = AdvancePhase(g.foamPhase, Mathf.Lerp(foamPoolSpeedRange.x, foamPoolSpeedRange.y, opening), deltaTime);

                if (g.waterRenderer) SetPhase(g.waterRenderer, g.waterPhase);
                if (g.foamPool) SetPhase(g.foamPool, g.foamPhase);
            }
        }

        /// <summary>
        /// 렌더러의 다른 MaterialPropertyBlock 값은 그대로 두고 흐름 위상만 바꾼다.
        /// </summary>
        private void SetPhase(Renderer renderer, float phase)
        {
            renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(PhaseId, phase);
            renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 기본 상태(전부 닫힘, 물 없음)로 되돌린다 — 연출 시작점.
        /// </summary>
        public void SetNeutral()
        {
            SetOpeningValue(0f);
            _flowReach = 0f;
            ApplyAll();
        }

        /// <summary>
        /// 에너지 효율(%)에 비례해 수문을 연다 — 0%면 닫힌 채, 100%면 완전 개방(수문 4개를 같은 양만큼).
        /// </summary>
        public async UniTask ApplyAsync(int percent, CancellationToken ct)
        {
            // 수문은 전 구간에 걸쳐 천천히 열리고, 물은 수문이 조금 열린 뒤에야 마루를 넘어 내려간다 —
            // 둘을 같이 움직이면 살짝 열린 순간 물이 이미 토우까지 닿아 뚝 끊기듯 보이기 때문이다.
            // 도달 거리는 최종 개방량과 무관하게 항상 1(토우)까지 간다.
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
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);

            SetOpeningValue(targetOpening);
            _flowReach = targetReach;
            ApplyAll();
        }

        /// <summary>
        /// 모든 수문의 개방량 값을 지정한다.
        /// </summary>
        private void SetOpeningValue(float opening)
        {
            _currentOpening = opening;
            if (gates == null) return;
            foreach (DamGate g in gates)
            {
                if (g != null) g.opening = opening;
            }
        }

        /// <summary>
        /// 모든 수문에 현재 개방량과 물줄기 도달 정도를 반영한다.
        /// </summary>
        private void ApplyAll()
        {
            if (gates == null) return;
            _mpb ??= new MaterialPropertyBlock();
            foreach (DamGate g in gates)
            {
                if (g != null) Apply(g);
            }
        }

        /// <summary>
        /// 수문 하나의 스케일·거품·포말·물줄기 셰이더 값을 개방량에 맞춘다.
        /// </summary>
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
                Vector2 startSpeed = Vector2.Lerp(minFoamStartSpeed, maxFoamStartSpeed, opening);
                fm.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed.x, startSpeed.y);
            }

            // 착수 포말 웅덩이 - 개방량만큼 진해지고 빨라진다
            if (g.foamPool)
            {
                g.foamPool.GetPropertyBlock(_mpb);
                _mpb.SetFloat(OpeningId, closed ? 0f : Mathf.Lerp(foamPoolOpeningRange.x, foamPoolOpeningRange.y, opening) * _flowReach);
                _mpb.SetFloat(PhaseId, g.foamPhase);
                g.foamPool.SetPropertyBlock(_mpb);
            }

            CacheWaterRenderers(g);

            // 두께 - 블렌드셰이프 "Thick" (많이 열릴수록 물이 두꺼워진다)
            SkinnedMeshRenderer smr = g.waterSkin;
            if (smr && smr.sharedMesh && smr.sharedMesh.blendShapeCount > ThickBlendShapeIndex)
            {
                float thick = closed ? 0f : Mathf.Lerp(minStreamThickness, 1f, opening);
                smr.SetBlendShapeWeight(ThickBlendShapeIndex, thick * MaxBlendShapeWeight);
            }

            // 물 - 폭/투명도/유속은 셰이더로 처리한다.
            // SetActive는 OnValidate 중 호출이 금지되어 있어, 닫히면 완전 투명으로 없앤다.
            Renderer r = g.waterRenderer;
            if (r)
            {
                r.GetPropertyBlock(_mpb);
                // 폭·투명도는 고정, 개방량에 따라 달라지는 건 두께(블렌드셰이프)와 유속(위상이 쌓이는 속도, AdvanceFlowPhases)뿐
                _mpb.SetFloat(WidthId, closed ? 0f : Mathf.Lerp(minStreamWidth, 1f, opening));
                _mpb.SetFloat(HeightId, closed ? 0f : Mathf.Lerp(minStreamHeight, 1f, opening));
                _mpb.SetFloat(FlowFracId, closed ? 0f : _flowReach);
                _mpb.SetFloat(OpeningId, closed ? 0f : 1f);
                _mpb.SetFloat(PhaseId, g.waterPhase);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// 물줄기 오브젝트의 렌더러를 찾아 둔다 — 인스펙터에서 water를 바꾼 경우에만 다시 찾는다.
        /// </summary>
        private static void CacheWaterRenderers(DamGate g)
        {
            if (g.cachedWater == g.water) return;

            g.cachedWater = g.water;
            g.water.TryGetComponent(out g.waterSkin);
            g.water.TryGetComponent(out g.waterRenderer);
        }
    }
}
