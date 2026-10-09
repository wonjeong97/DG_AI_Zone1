using UnityEngine;
using UnityEngine.InputSystem;

namespace Admin
{
    // 관리자 창들이 함께 쓰는 무입력 자동 닫기 타이머 — 관리자가 창을 열어 둔 채 자리를 뜨면 관람객이 설정을 바꿀 수 있어 닫는다.
    // 타이틀은 비활동 타이머가 멈춰 있어 이 타이머가 없으면 창이 계속 열려 있다.
    // 화면 어디든 누르면 다시 잰다. 바코드 스캐너는 키보드처럼 글자를 보내므로 키 입력으로는 다시 재지 않는다 —
    // 관리자가 자리를 떠도 관람객이 QR을 찍을 때마다 창이 계속 열려 있게 되는 것을 막는다(터치 전시라 키보드 조작은 없다).
    public sealed class IdleCloseTimer
    {
        private float _lastInputTime;

        /// <summary>
        /// 지금부터 다시 잰다 (창을 열 때).
        /// </summary>
        public void Restart() => _lastInputTime = Time.unscaledTime;

        /// <summary>
        /// 이번 프레임의 누르기 입력을 반영한 뒤, 마지막 입력에서 timeoutSeconds가 지났는지 반환한다 (창의 Update에서 매 프레임 부른다).
        /// </summary>
        public bool HasExpired(float timeoutSeconds) => HasExpired(timeoutSeconds, IsPressedThisFrame());

        /// <summary>
        /// 이번 프레임에 눌렀는지를 받아 반영한 뒤, 마지막 입력에서 timeoutSeconds가 지났는지 반환한다 (입력 장치 없이 검증할 때 쓴다).
        /// </summary>
        public bool HasExpired(float timeoutSeconds, bool pressedThisFrame)
        {
            if (pressedThisFrame) Restart();
            return Time.unscaledTime - _lastInputTime >= timeoutSeconds;
        }

        /// <summary>
        /// 이번 프레임에 화면(마우스·터치)을 눌렀는지 반환한다.
        /// </summary>
        private static bool IsPressedThisFrame()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }
    }
}
