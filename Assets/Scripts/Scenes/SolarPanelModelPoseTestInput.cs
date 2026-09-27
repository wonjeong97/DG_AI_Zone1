using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Scenes
{
    // 테스트 전용(TestScene) — SolarPanelModelPose 수동 테스트용 키 입력.
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

        /// <summary>
        /// 같은 오브젝트의 SolarPanelModelPose를 캐싱한다.
        /// </summary>
        private void Awake()
        {
            if (!TryGetComponent(out _pose))
                Debug.LogError($"[SolarPanelModelPoseTestInput] {name}에 SolarPanelModelPose가 없습니다.");
        }

        /// <summary>
        /// 숫자 키로 방향·각도를, R 키로 중립 자세를 지정한다.
        /// </summary>
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

        /// <summary>
        /// 방향을 바꾸고 현재 각도와 함께 적용한다.
        /// </summary>
        private void SetDirection(string direction)
        {
            currentDirection = direction;
            StartApply();
        }

        /// <summary>
        /// 각도를 바꾸고 현재 방향과 함께 적용한다.
        /// </summary>
        private void SetAngle(string angle)
        {
            currentAngle = angle;
            StartApply();
        }

        /// <summary>
        /// 진행 중인 자세 연출을 취소하고, 파괴 토큰과 링크한 새 CTS로 연출을 시작한다.
        /// </summary>
        private void StartApply()
        {
            if (!_pose) return;

            CancelApply();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ApplyAsync(_cts).Forget(); // 토큰이 아니라 CTS 객체를 넘긴다
        }

        /// <summary>
        /// 현재 방향·각도로 자세 연출을 재생하고, 끝나면 자기 CTS일 때만 정리한다.
        /// </summary>
        private async UniTaskVoid ApplyAsync(CancellationTokenSource cts)
        {
            try
            {
                await _pose.ApplyAsync(currentAngle, currentDirection, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 새 입력이나 파괴로 취소된 정상 흐름
            }
            finally
            {
                // 필드가 아직 자기 CTS일 때만 정리한다 — 새 연출이 이미 교체했다면 그쪽 CTS를 폐기하면 안 됨
                if (_cts == cts)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }

        /// <summary>
        /// 진행 중인 연출을 취소하고 CTS를 해제한다.
        /// </summary>
        private void CancelApply()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        /// <summary>
        /// 진행 중인 연출을 멈추고 중립 자세로 되돌린다.
        /// </summary>
        private void ResetNeutral()
        {
            CancelApply();
            if (_pose) _pose.SetNeutral();
        }

        /// <summary>
        /// 진행 중인 연출을 취소한다.
        /// </summary>
        private void OnDestroy()
        {
            CancelApply();
        }
    }
}
