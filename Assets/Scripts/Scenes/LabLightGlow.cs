using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace Scenes
{
    // 결과 씬(레벨5) 연구소 — 건물 안 노란 조명으로 전력 수급 상태를 보여준다.
    // 결과 텍스트와 같은 경계로 나눈다 — 부족: 꺼짐 / 보통: 약하게 켜져 불안정하게 깜빡임 / 양호: 강하게 켜짐.
    // SetNeutral()로 꺼 둔 뒤 ApplyAsync(효율%)로 그 단계 밝기까지 서서히 올린다 —
    // 태양광 SolarPanelModelPose(자세), 풍력 WindTurbineSpin(속도), 수력 DamGateFlow(수문 개방),
    // 발전소 PowerPlantPump(피스톤)와 같은 자리에 대응한다.
    public class LabLightGlow : MonoBehaviour
    {
        [Tooltip("건물 안에 둔 조명들 — 모두 같은 세기로 켜진다. 비워 두면 이 오브젝트와 바로 아래 자식의 조명을 쓴다")]
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

        // 깜빡임을 시작한 뒤 흐른 시간 — 앱 가동 시간(Time.time)을 그대로 노이즈에 넣으면 며칠 켜 둔 뒤 float 정밀도가 떨어져 깜빡임이 계단식이 된다
        private float _flickerTime;
        private ILogger<LabLightGlow> _logger;

        /// <summary>
        /// 로거를 주입받는다 (결과 씬을 불러올 때 GameLifetimeScope가 주입).
        /// </summary>
        [Inject]
        public void Construct(ILogger<LabLightGlow> logger) => _logger = logger;

        /// <summary>
        /// 조명 배열이 비어 있으면 이 오브젝트와 바로 아래 자식의 조명을 한 번만 모아 둔다 — 매 프레임 찾지 않는다.
        /// </summary>
        private void Awake()
        {
            if (lights == null || lights.Length == 0) lights = CollectOwnLights();
        }

        /// <summary>
        /// 쓸 조명이 하나도 없으면 경고한다 (주입은 Awake 뒤라 Start에서 남긴다).
        /// </summary>
        private void Start()
        {
            if (lights.Length == 0 && _logger != null)
                _logger.ZLogWarning($"[LabLightGlow] {name}에 조명이 없어 연구소 조명 연출이 보이지 않습니다.");
        }

        /// <summary>
        /// 이 오브젝트와 바로 아래 자식에 붙은 조명을 모은다.
        /// </summary>
        private Light[] CollectOwnLights()
        {
            List<Light> found = new List<Light>();
            if (TryGetComponent(out Light own)) found.Add(own);
            foreach (Transform child in transform)
                if (child.TryGetComponent(out Light childLight)) found.Add(childLight);
            return found.ToArray();
        }

        /// <summary>
        /// 보통 단계에서 밝기를 흔들고, 불규칙한 간격으로 잠깐씩 끈다.
        /// </summary>
        private void Update()
        {
            if (!_flickering) return;

            _flickerTime += Time.deltaTime;
            float now = _flickerTime;
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
        /// 효율(%)을 단계 조명 세기로 바꾼다 — 부족 0 / 보통 weakIntensity / 양호 strongIntensity.
        /// </summary>
        private float ToIntensity(int percent)
        {
            // 경계는 결과 텍스트와 같은 기준을 쓴다.
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
            _flickerTime = 0f;
            _dropoutEndAt = 0f;
            _nextDropoutAt = Random.Range(dropoutInterval.x, dropoutInterval.y);
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
        /// 조명 세기를 모든 조명에 반영한다 — 0이면 조명을 꺼 그림자 계산도 하지 않는다.
        /// </summary>
        private void ApplyIntensity(float intensity)
        {
            foreach (Light l in lights)
            {
                if (!l) continue;
                l.intensity = intensity;
                l.enabled = intensity > 0f;
            }
        }
    }
}
