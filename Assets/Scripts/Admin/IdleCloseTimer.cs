using UnityEngine;
using UnityEngine.InputSystem;

namespace Admin
{
    // 관리자 창들이 함께 쓰는 무입력 자동 닫기 타이머 — 관리자가 창을 열어 둔 채 자리를 뜨면 관람객이 설정을 바꿀 수 있어 닫는다.
    // 타이틀은 비활동 타이머가 멈춰 있어 이 타이머가 없으면 창이 계속 열려 있다.
    // 화면 어디든 누르거나 키를 누르면 다시 잰다.
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
        public bool HasExpired(float timeoutSeconds)
        {
            if (IsAnyPressedThisFrame()) Restart();
            return Time.unscaledTime - _lastInputTime >= timeoutSeconds;
        }

        /// <summary>
        /// 이번 프레임에 화면(마우스·터치)이나 키보드를 눌렀는지 반환한다.
        /// </summary>
        private static bool IsAnyPressedThisFrame()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) return true;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }
    }
}
