using System.Collections;
using Data;
using Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 실제 입력 모듈(InputSystemUIInputModule)에 가상 마우스·터치스크린 입력을 넣어 블록을 끌고, 끄는 동안 포인터를 잃은 것으로 오판하지 않는지 검증한다.
    ///
    /// 배경: 입력 모듈은 포인터마다 이벤트 하나를 왼쪽·오른쪽·가운데 버튼 처리에 돌려써 매 프레임 pointerDrag를 가운데 버튼의 빈 값으로 덮어쓴다.
    /// 이것을 끝 신호 없이 사라진 손가락으로 보면 블록이 끌자마자 원래 자리로 돌아갔다.
    /// </summary>
    public class BlockDragInputModuleTests : InputTestFixture
    {
        // 드래그 임계값(기본 10픽셀)을 넘기되 작은 게임 뷰에서도 코딩 영역 안에 머무는 거리
        private const float DragDistance = 60f;

        private UiRaycastStage _stage;
        private CodingZone _zone;
        private CodingBlock _block;

        /// <summary>
        /// 입력 모듈이 달린 EventSystem과 화면 전체를 덮는 코딩 영역, 그 가운데에 터치를 받는 블록 하나를 준비한다.
        /// </summary>
        [SetUp]
        public void SetUpStage()
        {
            // 게임 뷰에 포커스가 없어도 입력 모듈이 포인터를 처리하게 한다(설정은 InputTestFixture가 테스트 뒤 되돌린다)
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            _stage = new UiRaycastStage(nameof(BlockDragInputModuleTests));
            _stage.EventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            EventSystem.current = _stage.EventSystem;

            _zone = BlockTestUtil.MakeZone();
            _zone.transform.SetParent(_stage.Canvas.transform, false);
            if (_zone.TryGetComponent(out RectTransform zoneRect))
            {
                zoneRect.anchorMin = Vector2.zero;
                zoneRect.anchorMax = Vector2.one;
                zoneRect.sizeDelta = Vector2.zero;
            }

            _block = BlockTestUtil.MakeBlock(_zone, "A", BlockCategory.Command, _zone.transform);
            _block.Init(BlockCategory.Command, _stage.Canvas);
            _block.gameObject.AddComponent<Image>();
        }

        /// <summary>
        /// 테스트가 만든 오브젝트를 모두 파괴한다(코딩 영역과 블록은 캔버스와 함께 사라진다).
        /// </summary>
        [TearDown]
        public void TearDownStage()
        {
            _stage.Destroy();
        }

        /// <summary>
        /// 마우스로 끄는 블록은 움직이지 않는 프레임에도 원래 자리로 돌아가지 않고, 버튼을 떼면 코딩 영역에 놓인다.
        /// </summary>
        [UnityTest]
        public IEnumerator 마우스로_끄는_블록은_버튼을_뗄_때까지_끌린다()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Vector2 start = BlockScreenPoint();
            Vector2 end = start + new Vector2(DragDistance, 0f);

            Set(mouse.position, start);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Set(mouse.position, end);
            yield return null;
            yield return null;

            Assert.AreSame(_stage.Canvas.transform, _block.transform.parent, "마우스로 끄는 중인 블록이 원래 자리로 돌아감");
            Assert.AreEqual(end.x, BlockScreenPoint().x, 1f, "블록이 마우스를 따라 움직이지 않음");

            Release(mouse.leftButton);
            yield return null;

            Assert.AreSame(_zone.transform, _block.transform.parent, "버튼을 뗀 블록이 코딩 영역에 놓이지 않음");
            Assert.AreEqual(end.x, BlockScreenPoint().x, 1f, "놓은 블록이 마우스를 뗀 자리에 있지 않음");
        }

        /// <summary>
        /// 터치로 끄는 블록은 움직이지 않는 프레임에도 원래 자리로 돌아가지 않고, 손가락을 떼면 코딩 영역에 놓인다.
        /// </summary>
        [UnityTest]
        public IEnumerator 터치로_끄는_블록은_손가락을_뗄_때까지_끌린다()
        {
            InputSystem.AddDevice<Touchscreen>();
            Vector2 start = BlockScreenPoint();
            Vector2 end = start + new Vector2(DragDistance, 0f);

            BeginTouch(1, start);
            yield return null;
            MoveTouch(1, end);
            yield return null;
            yield return null;

            Assert.AreSame(_stage.Canvas.transform, _block.transform.parent, "터치로 끄는 중인 블록이 원래 자리로 돌아감");
            Assert.AreEqual(end.x, BlockScreenPoint().x, 1f, "블록이 손가락을 따라 움직이지 않음");

            EndTouch(1, end);
            yield return null;

            Assert.AreSame(_zone.transform, _block.transform.parent, "손가락을 뗀 블록이 코딩 영역에 놓이지 않음");
        }

        /// <summary>
        /// 끄던 중 터치스크린이 사라지면(입력 모듈이 끝 신호 없이 포인터를 지움) 블록이 원래 자리로 돌아가 다시 터치를 받는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 끌던_터치스크린이_사라지면_원래_자리로_돌아간다()
        {
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            Vector2 start = BlockScreenPoint();

            BeginTouch(1, start);
            yield return null;
            MoveTouch(1, start + new Vector2(DragDistance, 0f));
            yield return null;
            Assert.AreSame(_stage.Canvas.transform, _block.transform.parent, "테스트 전제: 블록을 끌고 있음");

            // 다음 프레임 Update에서 입력 모듈이 포인터를 지우고 그 프레임 LateUpdate에서 블록이 돌아가므로, 코루틴이 재개되는 Update 뒤를 한 번 더 넘긴다
            InputSystem.RemoveDevice(touchscreen);
            yield return null;
            yield return null;

            Assert.AreSame(_zone.transform, _block.transform.parent, "사라진 손가락의 블록이 캔버스 위에 떠 있음");
            Assert.AreEqual(start.x, BlockScreenPoint().x, 1f, "블록이 끌기 전 자리로 돌아가지 않음");
            Assert.IsTrue(_block.TryGetComponent(out CanvasGroup group) && group.blocksRaycasts, "돌아간 블록이 터치를 받지 않음");
        }

        /// <summary>
        /// 블록 가운데의 화면 좌표(오버레이 캔버스라 카메라 없음).
        /// </summary>
        private Vector2 BlockScreenPoint() => RectTransformUtility.WorldToScreenPoint(null, _block.transform.position);
    }
}
