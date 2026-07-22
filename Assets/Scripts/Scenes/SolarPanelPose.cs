using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DG.Scenes
{
    // 결과 씬 태양광 패널 — 각도(기울기)·방향(회전) 값에 따라 자세를 잡는다.
    // 기울기·방향 모두 tiltPivot(PanelPivot)의 local X·Y로 표현.
    // 바닥(Base, Pole)은 움직이지 않는다.
    public class SolarPanelPose : MonoBehaviour
    {
        [SerializeField] private Transform tiltPivot;      // 기울기·방향을 제어할 피벗(PanelPivot)
        [SerializeField] private Transform pole;           // 기둥(Pole) — 틸트 시 높이를 줄여 패널 관통 방지
        [SerializeField] private float animDuration = 1.5f;

        private const float FrontYaw = 180f;   // 카메라 정면 기준 yaw
        private const float DefaultTilt = -30f;

        // Pole 초기값 (Awake에서 캐시)
        private float _poleBaseY;    // Pole 하단 월드-로컬 Y
        private float _poleOrigScaleY;
        private float _pivotY;       // PanelPivot의 local Y (= Pole 상단 한계)
        private bool _isNorth;       // 북쪽 방향 판정 플래그 (북쪽일 경우 패널 뒷면이 보이므로 기둥 조절 안함)

        private void Awake()
        {
            if (pole)
            {
                _poleOrigScaleY = pole.localScale.y;
                // Pole 하단 = localPosition.y - (scaleY * 0.5)  (Cube 기준)
                _poleBaseY = pole.localPosition.y - _poleOrigScaleY * 0.5f;
            }
            if (tiltPivot)
                _pivotY = tiltPivot.localPosition.y;
        }

        // 방향 문자열 → PanelPivot local Y (root 기준 상대값).
        // root가 이미 FrontYaw(180°)를 향하므로, 남쪽=0, 동쪽=-90, …
        private static float DirectionToLocalYaw(string direction)
        {
            float worldYaw = direction switch
            {
                "북쪽" => 0f,
                "동쪽" => 90f,
                "남쪽" => 180f,
                "서쪽" => 270f,
                _ => FrontYaw,
            };
            return worldYaw - FrontYaw;
        }

        // 각도 문자열("30도") → 기울기(local X, 음수). 값 없으면 기본값
        private static float AngleToTilt(string angle)
        {
            if (!string.IsNullOrEmpty(angle))
            {
                string digits = new string(System.Array.FindAll(angle.ToCharArray(), char.IsDigit));
                if (int.TryParse(digits, out int deg) && deg > 0)
                    return -deg;
            }
            return DefaultTilt;
        }

        // 기본 자세 (평평 + 정면) — 연출 시작점. 바닥은 고정.
        public void SetNeutral()
        {
            _isNorth = false;
            if (tiltPivot)
            {
                Vector3 e = tiltPivot.localEulerAngles;
                e.x = 0f;
                e.y = 0f;
                tiltPivot.localEulerAngles = e;
            }
            RestorePole();
        }

        // 값에 맞춰 애니메이션으로 자세 변경 — 방향 → 각도 순차 재생.
        public async UniTask ApplyAsync(string angle, string direction, CancellationToken ct)
        {
            _isNorth = (direction == "북쪽");
            float targetTilt = AngleToTilt(angle);
            float targetYaw = DirectionToLocalYaw(direction);
            float currentTilt = GetCurrentTilt();

            // 1) 방향(yaw) 먼저 — DeltaAngle로 최단 경로 회전
            float startYaw = tiltPivot ? tiltPivot.localEulerAngles.y : 0f;
            float yaw = startYaw;
            float endYaw = startYaw + Mathf.DeltaAngle(startYaw, targetYaw);
            await DOTween.To(() => yaw, y =>
                {
                    yaw = y;
                    SetPivot(currentTilt, y);
                }, endYaw, animDuration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .WithCancellation(ct);
            SetPivot(currentTilt, targetYaw);

            // 2) 각도(tilt)
            float tilt = currentTilt;
            await DOTween.To(() => tilt, x =>
                {
                    tilt = x;
                    SetPivot(x, targetYaw);
                }, targetTilt, animDuration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .WithCancellation(ct);
            SetPivot(targetTilt, targetYaw);
        }

        private float GetCurrentTilt()
        {
            if (!tiltPivot) return 0f;
            float x = tiltPivot.localEulerAngles.x;
            return x > 180f ? x - 360f : x;
        }

        private void SetPivot(float tilt, float yaw)
        {
            if (tiltPivot)
            {
                Vector3 e = tiltPivot.localEulerAngles;
                e.x = tilt;
                e.y = yaw;
                tiltPivot.localEulerAngles = e;
            }
            
            if (_isNorth)
            {
                RestorePole();
            }
            else
            {
                AdjustPole(tilt);
            }
        }

        // 틸트 각도에 따라 Pole 상단이 패널 아래에 머무르도록 높이를 줄인다.
        private void AdjustPole(float tiltDeg)
        {
            if (!pole) return;

            // 패널이 기울어지면 뒷부분(+Z 방향)이 아래로 내려온다.
            // Frame 반폭(Z) ≈ 0.78/2 = 0.39 → 패널 뒤쪽 최하점
            //   dropY = sin(|tilt|) * halfDepth
            // Pole 상단 한계 = pivotY - dropY - 여유(gap)
            float absTilt = Mathf.Abs(tiltDeg);
            const float halfDepth = 0.39f; // Frame scaleZ/2
            const float gap = 0.02f;       // 약간의 여유
            float dropY = Mathf.Sin(absTilt * Mathf.Deg2Rad) * halfDepth;
            float maxTopY = _pivotY - dropY - gap;

            // 새 scaleY = (maxTopY - poleBaseY). 원래 높이보다 커지지 않도록 clamp
            float newScaleY = Mathf.Clamp(maxTopY - _poleBaseY, 0.05f, _poleOrigScaleY);
            Vector3 s = pole.localScale;
            s.y = newScaleY;
            pole.localScale = s;

            // 중심 Y 재계산 (Cube pivot = center)
            Vector3 p = pole.localPosition;
            p.y = _poleBaseY + newScaleY * 0.5f;
            pole.localPosition = p;
        }

        private void RestorePole()
        {
            if (!pole) return;
            Vector3 s = pole.localScale;
            s.y = _poleOrigScaleY;
            pole.localScale = s;
            Vector3 p = pole.localPosition;
            p.y = _poleBaseY + _poleOrigScaleY * 0.5f;
            pole.localPosition = p;
        }
    }
}
