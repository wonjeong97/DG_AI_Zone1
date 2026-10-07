using UnityEngine.InputSystem;

namespace App
{
    /// <summary>
    /// 템플릿 디버그 단축키(D 디버그 창·I 인스펙터·M 마우스 커서)를 Ctrl 조합(Ctrl+D·Ctrl+I·Ctrl+M)으로 바꾼다.
    /// 타이틀의 QR 스캐너는 키보드처럼 uid의 영문 대문자를 입력하므로, 단일 문자 키면 스캔 중에 디버그 기능이 켜진다.
    /// 스캐너는 대문자에 Shift만 붙이고 Ctrl은 보내지 않으므로 Ctrl 조합은 겹치지 않는다.
    /// 템플릿 패키지는 고치지 않고 런타임 바인딩 오버라이드로 바꾼다.
    /// </summary>
    public static class DebugShortcutBindings
    {
        private const string CtrlPath = "<Keyboard>/ctrl";

        /// <summary>
        /// 세 단축키의 단일 키 바인딩을 끄고 같은 키에 Ctrl 조합 바인딩을 추가한다.
        /// 액션이 이미 켜져 있어도 Input System이 바인딩을 다시 잡으므로 호출 시점은 상관없다.
        /// </summary>
        public static void Apply(TemplateInputActions actions)
        {
            RequireCtrl(actions.System.ToggleDebug);
            RequireCtrl(actions.System.ToggleInspector);
            RequireCtrl(actions.System.ToggleMouse);
        }

        /// <summary>
        /// 템플릿 원본 바인딩(첫 번째)을 빈 경로로 덮어써 끄고, 그 키를 Ctrl과 함께 눌러야 하는 조합을 추가한다.
        /// </summary>
        private static void RequireCtrl(InputAction action)
        {
            string keyPath = action.bindings[0].path;
            action.ApplyBindingOverride(0, string.Empty);
            action.AddCompositeBinding("OneModifier")
                .With("Modifier", CtrlPath)
                .With("Binding", keyPath);
        }
    }
}
