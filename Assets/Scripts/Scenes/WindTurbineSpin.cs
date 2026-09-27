using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Scenes
{
    // 풍차 블레이드 회전 애니메이션 — 나셀(Cylinder.002/006)을 로컬 X축 음의 방향으로 계속 회전시킨다.
    // Y/Z는 90도로 고정 유지 — Rotate()의 상대 회전 누적 대신 매 프레임 절대값으로 재구성해 오차 없이 고정한다.
    //
    // 결과 씬(레벨2)에서는 회전 '속도'가 발전 결과를 보여주는 연출이다.
    // SetNeutral()로 멈춰 세운 뒤 ApplyAsync(효율%)로 서서히 속도를 올린다 —
    // 태양광 패널의 SolarPanelModelPose(방향/각도)와 같은 자리에 대응한다.
    public class WindTurbineSpin : MonoBehaviour
    {
        [SerializeField] private Transform cylinder002;
        [SerializeField] private Transform cylinder006;
        [SerializeField] private float speed = 90f;         // 효율 100%일 때의 초당 회전 각도(도)
        [SerializeField] private float rampDuration = 1.5f; // 정지 → 목표 속도까지 걸리는 시간(초)

        // 4_Result.json의 turbineSpinDuration으로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float RampDuration
        {
            get => rampDuration;
            set => rampDuration = value;
        }

        private const float FixedYZ = 90f;
        private const float MaxPercent = 100f;

        private float _angle;
        private float _currentSpeed;

        /// <summary>
        /// 결과 연출을 쓰지 않는 씬(전시용 배치 등)에서는 인스펙터 속도로 그냥 돌아가도록 초기 속도를 지정한다.
        /// </summary>
        private void Awake() => _currentSpeed = speed;

        /// <summary>
        /// 현재 속도만큼 블레이드 각도를 누적해 두 나셀의 회전을 재구성한다.
        /// </summary>
        private void Update()
        {
            _angle -= _currentSpeed * Time.deltaTime;
            Quaternion rot = Quaternion.Euler(_angle, FixedYZ, FixedYZ);
            if (cylinder002) cylinder002.localRotation = rot;
            if (cylinder006) cylinder006.localRotation = rot;
        }

        /// <summary>
        /// 기본 상태(정지)로 되돌린다 — 연출 시작점.
        /// </summary>
        public void SetNeutral() => _currentSpeed = 0f;

        /// <summary>
        /// 에너지 효율(%)에 비례해 회전 속도를 올린다 — 0%면 멈춘 채, 100%면 speed 그대로.
        /// </summary>
        public async UniTask ApplyAsync(int percent, CancellationToken ct)
        {
            float target = speed * Mathf.Clamp01(percent / MaxPercent);
            await DOVirtual.Float(_currentSpeed, target, rampDuration, v => _currentSpeed = v)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            _currentSpeed = target;
        }
    }
}
