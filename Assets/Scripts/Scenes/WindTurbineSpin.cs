using UnityEngine;

namespace Scenes
{
    // 풍차 블레이드 회전 애니메이션 — 나셀(Cylinder.002/006)을 로컬 X축 음의 방향으로 계속 회전시킨다.
    // Y/Z는 90도로 고정 유지 — Rotate()의 상대 회전 누적 대신 매 프레임 절대값으로 재구성해 오차 없이 고정한다.
    public class WindTurbineSpin : MonoBehaviour
    {
        [SerializeField] private Transform cylinder002;
        [SerializeField] private Transform cylinder006;
        [SerializeField] private float speed = 90f; // 초당 회전 각도(도)

        private const float FixedYZ = 90f;

        private float _angle;

        private void Update()
        {
            _angle -= speed * Time.deltaTime;
            var rot = Quaternion.Euler(_angle, FixedYZ, FixedYZ);
            if (cylinder002) cylinder002.localRotation = rot;
            if (cylinder006) cylinder006.localRotation = rot;
        }
    }
}
