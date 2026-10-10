using NUnit.Framework;
using Scenes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 연출 스킵 판정이 씬 전환 중의 터치를 받지 않는지 가상 터치스크린으로 검증한다.
    ///
    /// 배경: 페이드 커튼은 UI 레이캐스트만 막아, 타이틀 시작하기·결과 다음을 연타하던 터치가
    /// 새 씬의 인트로 이름·아웃트로 엔딩 문구 연출을 화면이 보이기 전에 건너뛰게 했다.
    /// </summary>
    public class StoryLineSkipPressTests : InputTestFixture
    {
        /// <summary>
        /// 씬 전환 중이 아니면 이번 프레임에 누른 터치를 스킵으로 본다.
        /// </summary>
        [Test]
        public void 전환_중이_아니면_누른_터치를_스킵으로_본다()
        {
            InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f));

            Assert.IsFalse(SceneFader.IsLoading, "테스트 전제: 씬 전환 중이 아님");
            Assert.IsTrue(StoryLineAnimator.IsPointerPressedThisFrame(), "전환 중이 아닌데 누른 터치를 스킵으로 보지 않음");
        }

        /// <summary>
        /// 씬 전환 중에 누른 터치는 다음 씬의 연출을 건너뛰지 않도록 스킵으로 보지 않는다.
        /// </summary>
        [Test]
        public void 전환_중에_누른_터치는_스킵으로_보지_않는다()
        {
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f));

            Assert.IsTrue(touchscreen.press.wasPressedThisFrame, "테스트 전제: 이번 프레임에 터치가 눌림");
            Assert.IsFalse(StoryLineAnimator.IsSkipPress(touchscreen, true), "씬 전환 중에 누른 터치를 스킵으로 봄");
        }
    }
}
