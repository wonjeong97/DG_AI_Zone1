using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

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

        private const float FrontYaw = 180f;   // 카메라 정면 기준 yaw
        private const float BaselineAngle = 45f; // 이 각도일 때 tiltPivot local Y = 0

        private Quaternion _yawRestRotation;
        private bool _yawRestCaptured;
        private float _currentYaw;
        private float _currentTilt;

        private void Awake()
        {
            CaptureYawRest();
        }

        // Awake가 아직 실행되지 않은 상태(예: 에디터 스크립트로 Play 모드 없이 직접 호출)에서도
        // yawPivot의 원래 회전값을 안전하게 확보한다.
        private void CaptureYawRest()
        {
            if (_yawRestCaptured || !yawPivot) return;
            _yawRestRotation = yawPivot.rotation;
            _yawRestCaptured = true;
        }

        // 방향 문자열 → yaw 각도(root 기준 상대값).
        // root가 이미 FrontYaw(180°)를 향하므로, 남쪽=0, 동쪽=-90, …
        private static float DirectionToLocalYaw(string direction)
        {
            float worldYaw = direction switch
            {
                Constants.Directions.North => 0f,
                Constants.Directions.East  => 90f,
                Constants.Directions.South => 180f,
                Constants.Directions.West  => 270f,
                _ => FrontYaw,
            };
            return worldYaw - FrontYaw;
        }

        // 각도 문자열("30도") → tiltPivot local Y. 45도 기준 오프셋. 값 없으면 0(=45도 취급)
        private static float AngleToTilt(string angle)
        {
            if (!string.IsNullOrEmpty(angle))
            {
                string digits = new string(System.Array.FindAll(angle.ToCharArray(), char.IsDigit));
                if (int.TryParse(digits, out int deg) && deg > 0)
                    return deg - BaselineAngle;
            }
            return 0f;
        }

        // 기본 자세 (평평 + 정면) — 연출 시작점.
        public void SetNeutral()
        {
            SetYaw(0f);
            SetTilt(0f);
        }

        // 값에 맞춰 애니메이션으로 자세 변경 — 방향 → 각도 순차 재생.
        public async UniTask ApplyAsync(string angle, string direction, CancellationToken ct)
        {
            float targetTilt = AngleToTilt(angle);
            float targetYaw = DirectionToLocalYaw(direction);

            // 1) 방향(yaw) 먼저 — DeltaAngle로 최단 경로 회전, PanelYawPivot을 돌린다.
            float startYaw = _currentYaw;
            float endYaw = startYaw + Mathf.DeltaAngle(startYaw, targetYaw);
            await DOVirtual.Float(startYaw, endYaw, animDuration, SetYaw)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: ct);
            SetYaw(targetYaw);

            // 2) 각도(tilt) — Planes_Group의 local Y만 돈다. 지지대(Cylinder.007)는 움직이지 않는다.
            float startTilt = _currentTilt;
            await DOVirtual.Float(startTilt, targetTilt, animDuration, SetTilt)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .ToUniTask(cancellationToken: ct);
            SetTilt(targetTilt);
        }

        private void SetYaw(float yaw)
        {
            CaptureYawRest();
            _currentYaw = yaw;
            if (!yawPivot) return;
            yawPivot.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * _yawRestRotation;
        }

        private void SetTilt(float tilt)
        {
            _currentTilt = tilt;
            if (!tiltPivot) return;
            tiltPivot.localEulerAngles = new Vector3(0f, tilt, 0f);
        }
    }
}
