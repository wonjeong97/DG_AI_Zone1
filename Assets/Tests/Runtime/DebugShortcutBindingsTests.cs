using App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 템플릿 단축키가 문자 키 하나로는 실행되지 않고 Ctrl 조합으로만 실행되는지 검증한다.
    /// QR 스캐너가 uid의 영문 대문자(D·I·M·F 포함)를 키보드 입력으로 보내므로 문자 키 하나로 실행되면 안 된다.
    /// </summary>
    public class DebugShortcutBindingsTests
    {
        private InputSettings _originalSettings;
        private InputSettings _testSettings;
        private Keyboard _keyboard;
        private TemplateInputActions _actions;

        /// <summary>
        /// 포커스와 상관없이 입력을 게임에 보내는 설정으로 바꾸고, 가상 키보드를 붙이고, Ctrl 조합을 적용한 입력 액션을 켠다.
        /// Editor에서 게임 뷰에 포커스가 없으면 InputState.Change가 에디터 상태에 기록되어 액션이 입력을 보지 못한다.
        /// 프로젝트에 Input Settings 에셋이 없어 메모리의 기본 설정만 잠시 바꾸므로 파일은 바뀌지 않는다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _originalSettings = InputSystem.settings;
            _testSettings = Object.Instantiate(_originalSettings);
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testSettings;

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _actions = new TemplateInputActions();
            DebugShortcutBindings.Apply(_actions);
            _actions.Enable();
        }

        /// <summary>
        /// 입력 액션과 가상 키보드를 정리하고 원래 입력 설정으로 되돌린다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _actions.Disable();
            _actions.Dispose();
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings = _originalSettings;
            Object.DestroyImmediate(_testSettings);
        }

        /// <summary>
        /// 문자 키만 누르면 실행되지 않고, Ctrl을 누른 채 문자 키를 누르면 한 번 실행된다.
        /// 이벤트 큐를 거치지 않고 키보드 상태를 바로 바꿔 액션 콜백을 동기로 발생시킨다.
        /// </summary>
        [TestCase("ToggleDebug", Key.D)]
        [TestCase("ToggleInspector", Key.I)]
        [TestCase("ToggleMouse", Key.M)]
        [TestCase("ToggleFocusRestore", Key.F)]
        public void 문자_키만으로는_실행되지_않고_Ctrl_조합으로_실행된다(string actionName, Key key)
        {
            int performedCount = 0;
            _actions.asset.FindAction(actionName, throwIfNotFound: true).performed += _ => performedCount++;

            SetPressedKeys(key);
            SetPressedKeys();
            Assert.AreEqual(0, performedCount, "문자 키만 눌렀는데 실행됨");

            SetPressedKeys(Key.LeftCtrl);
            SetPressedKeys(Key.LeftCtrl, key);
            SetPressedKeys();
            Assert.AreEqual(1, performedCount, "Ctrl 조합으로 실행되지 않음");
        }

        /// <summary>
        /// 가상 키보드의 눌린 키를 지정한 키들로 바꾼다.
        /// </summary>
        private void SetPressedKeys(params Key[] keys) => InputState.Change(_keyboard, new KeyboardState(keys));
    }
}
