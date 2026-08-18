using System.Threading;
using UnityEngine;

namespace DG.Scenes
{
    // 테스트 전용 — SolarPanelModelPose 수동 테스트용 키 입력.
    // 방향: 1(남) 2(동) 3(북) 4(서). 각도: 5(30도) 6(45도) 7(60도).
    // 방향·각도는 마지막으로 지정한 값을 유지한 채 서로 독립적으로 적용된다.
    // R: 중립 자세로 리셋.
    [RequireComponent(typeof(SolarPanelModelPose))]
    public class SolarPanelModelPoseTestInput : MonoBehaviour
    {
        [SerializeField] private string currentAngle = "45도";                       // 초기(미조작) 상태 = 45도로 간주
        [SerializeField] private string currentDirection = Constants.Directions.South;

        private SolarPanelModelPose _pose;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _pose = GetComponent<SolarPanelModelPose>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetDirection(Constants.Directions.South);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetDirection(Constants.Directions.East);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetDirection(Constants.Directions.North);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetDirection(Constants.Directions.West);

            if (Input.GetKeyDown(KeyCode.Alpha5)) SetAngle("30도");
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetAngle("45도");
            if (Input.GetKeyDown(KeyCode.Alpha7)) SetAngle("60도");

            if (Input.GetKeyDown(KeyCode.R)) ResetNeutral();
        }

        private void SetDirection(string direction)
        {
            currentDirection = direction;
            Apply();
        }

        private void SetAngle(string angle)
        {
            currentAngle = angle;
            Apply();
        }

        private void Apply()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = _pose.ApplyAsync(currentAngle, currentDirection, _cts.Token);
        }

        private void ResetNeutral()
        {
            _cts?.Cancel();
            _pose.SetNeutral();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
        }
    }
}
