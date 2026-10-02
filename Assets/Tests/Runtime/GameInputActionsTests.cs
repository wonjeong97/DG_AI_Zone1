using App;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 디버그 단축키 입력 액션이 Space 키에 묶여 있는지 검증한다 — 레벨 선택(모든 레벨 해금)·게임(컴파일 검증)이 같은 액션을 쓴다.
    /// </summary>
    public class GameInputActionsTests
    {
        /// <summary>
        /// Debug.Shortcut 액션은 키보드 Space 하나에만 묶여 있다.
        /// </summary>
        [Test]
        public void 디버그_단축키는_스페이스에_묶여_있다()
        {
            using GameInputActions input = new GameInputActions();

            Assert.AreEqual(1, input.Debug.Shortcut.bindings.Count);
            Assert.AreEqual("<Keyboard>/space", input.Debug.Shortcut.bindings[0].path);
        }
    }
}
