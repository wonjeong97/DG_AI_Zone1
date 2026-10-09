using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace Scenes
{
    // 신규 FBX 솔라패널 모델(Circle.001/Cylinder.007-009/Plane.006-008) 전용 자세 제어.
    // 방향(yaw)은 yawPivot(PanelYawPivot)을 월드 Y(수직)축 기준으로 회전.
    // 기울기(tilt)는 tiltPivot(Planes_Group)의 로컬 Y축을 회전 —
    // 45도를 기준(Y=0)으로 삼아, 그보다 낮은/높은 각도는 45도와의 차이만큼 Y를 +/-로 돌린다.
    // (30도 → Y=-15, 45도 → Y=0, 60도 → Y=+15. 실사용 범위 -15~+15에서는 지지대와
    // 간섭이 없음을 확인했다 — 이 범위를 벗어나는 극단적인 각도는 지지대 높이 보정이 필요할 수 있다.)
    //
    // 계층: PanelYawPivot(yaw) ─ Cylinder.007(지지대, 고정) / Planes_Group(tilt) ─ Plane 6/7/8.
    // Cylinder.007과 Planes_Group은 형제 관계라, 기울기를 조절해도 지지대 위치/크기에 영향이 없다.
    //
    // 주의: yawPivot(PanelYawPivot)은 FBX 임포트 시 로컬 좌표축이 월드축과 어긋나게 구워져 있어
    // (local Y가 world up이 아님) localEulerAngles를 직접 건드리면 엉뚱한 축으로 돈다.
    // 그래서 Awake에서 캐시한 원래 회전(rest rotation) 위에 월드 Y축 회전을 곱하는 방식으로 처리한다.
    public class SolarPanelModelPose : MonoBehaviour
    {
        [SerializeField] private Transform yawPivot;        // 방향을 제어할 피벗(PanelYawPivot)
        [SerializeField] private Transform tiltPivot;       // 기울기를 제어할 피벗(Planes_Group)
        [SerializeField] private float animDuration = 1.5f;

        // 4_Result.json의 panelPoseDuration으로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float AnimDuration
        {
            get => animDuration;
            set => animDuration = value;
        }

        private const float BaselineAngle = 45f; // 이 각도일 때 tiltPivot local Y = 0

        private Quaternion _yawRestRotation;
        private bool _yawRestCaptured;
        private float _currentYaw;
        private float _currentTilt;
        private ILogger<SolarPanelModelPose> _logger;

        /// <summary>
        /// 로거를 주입받는다 (결과 씬을 불러올 때 GameLifetimeScope가 주입).
        /// </summary>
        [Inject]
        public void Construct(ILogger<SolarPanelModelPose> logger) => _logger = logger;

        /// <summary>
        /// 방향 피벗의 원래 회전값을 기록한다.
        /// </summary>
        private void Awake()
        {
            CaptureYawRest();
        }

        /// <summary>
        /// 연출에 필요한 피벗이 빠져 있으면 경고한다 (주입은 Awake 뒤라 Start에서 남긴다).
        /// </summary>
        private void Start()
        {
            if (_logger == null) return;
            if (!yawPivot) _logger.ZLogWarning($"[SolarPanelModelPose] {name}에 yawPivot이 할당되지 않아 패널 방향이 바뀌지 않습니다.");
            if (!tiltPivot) _logger.ZLogWarning($"[SolarPanelModelPose] {name}에 tiltPivot이 할당되지 않아 패널 기울기가 바뀌지 않습니다.");
        }

        /// <summary>
        /// Awake가 아직 실행되지 않은 상태(예: 에디터 스크립트로 Play 모드 없이 직접 호출)에서도 yawPivot의 원래 회전값을 안전하게 확보한다.
        /// </summary>
        private void CaptureYawRest()
        {
            if (_yawRestCaptured || !yawPivot) return;
            _yawRestRotation = yawPivot.rotation;
            _yawRestCaptured = true;
        }

        /// <summary>
        /// 각도 문자열("30도")을 tiltPivot local Y(45도 기준 오프셋)로 바꾼다. 값이 없으면 0(=45도 취급).
        /// </summary>
        private static float AngleToTilt(string angle)
            => PanelPoseMath.TryParseAngleDegrees(angle, out int deg) ? deg - BaselineAngle : 0f;

        /// <summary>
        /// 기본 자세(평평 + 정면)로 되돌린다 — 연출 시작점.
        /// </summary>
        public void SetNeutral()
        {
            SetYaw(0f);
            SetTilt(0f);
        }

        /// <summary>
        /// 값에 맞춰 애니메이션으로 자세를 바꾼다 — 방향 → 각도 순차 재생.
        /// </summary>
        public async UniTask ApplyAsync(string angle, string direction, CancellationToken ct)
        {
            float targetTilt = AngleToTilt(angle);
            float targetYaw = PanelPoseMath.DirectionToLocalYaw(direction);

            // 1) 방향(yaw) 먼저 — DeltaAngle로 최단 경로 회전, PanelYawPivot을 돌린다.
            float startYaw = _currentYaw;
            float endYaw = startYaw + Mathf.DeltaAngle(startYaw, targetYaw);
            await DOVirtual.Float(startYaw, endYaw, animDuration, SetYaw)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            SetYaw(targetYaw);

            // 2) 각도(tilt) — Planes_Group의 local Y만 돈다. 지지대(Cylinder.007)는 움직이지 않는다.
            float startTilt = _currentTilt;
            await DOVirtual.Float(startTilt, targetTilt, animDuration, SetTilt)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            SetTilt(targetTilt);
        }

        /// <summary>
        /// 원래 회전 위에 월드 Y축 회전을 곱해 방향을 지정한다.
        /// </summary>
        private void SetYaw(float yaw)
        {
            CaptureYawRest();
            _currentYaw = yaw;
            if (!yawPivot) return;
            yawPivot.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * _yawRestRotation;
        }

        /// <summary>
        /// 기울기 피벗의 local Y를 지정한다.
        /// </summary>
        private void SetTilt(float tilt)
        {
            _currentTilt = tilt;
            if (!tiltPivot) return;
            tiltPivot.localEulerAngles = new Vector3(0f, tilt, 0f);
        }
    }
}
