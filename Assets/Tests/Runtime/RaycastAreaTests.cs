using System.Collections;
using System.Collections.Generic;
using App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 그리지 않고 터치만 받는 RaycastArea가 새 오브젝트에 붙여도 레이캐스트에 잡히는지 검증한다.
    /// </summary>
    public class RaycastAreaTests
    {
        private GameObject _eventSystemGo;
        private EventSystem _eventSystem;
        private GameObject _canvasGo;

        /// <summary>
        /// 레이캐스트에 필요한 EventSystem과 화면 오버레이 캔버스를 준비한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _eventSystemGo = new GameObject("RaycastAreaTests_EventSystem", typeof(EventSystem));
            _eventSystemGo.TryGetComponent(out _eventSystem);
            _canvasGo = new GameObject("RaycastAreaTests_Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _canvasGo.TryGetComponent(out Canvas canvas);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>
        /// 테스트가 만든 오브젝트를 모두 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_canvasGo) Object.Destroy(_canvasGo);
            if (_eventSystemGo) Object.Destroy(_eventSystemGo);
        }

        /// <summary>
        /// CanvasRenderer 없이 만든 오브젝트에 붙여도 CanvasRenderer가 함께 붙어, 누르면 그 오브젝트가 맞는다.
        /// (CanvasRenderer가 없으면 GraphicRaycaster가 MissingComponentException을 낸다)
        /// </summary>
        [UnityTest]
        public IEnumerator 새_오브젝트에_붙여도_레이캐스트에_잡힌다()
        {
            GameObject go = new GameObject("Area", typeof(RectTransform));
            go.transform.SetParent(_canvasGo.transform, false);
            go.AddComponent<RaycastArea>();
            Assert.IsTrue(go.TryGetComponent(out CanvasRenderer _), "CanvasRenderer가 함께 붙어야 함");

            go.TryGetComponent(out RectTransform rt);
            rt.sizeDelta = new Vector2(200f, 200f);

            yield return null;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.position);
            PointerEventData pointer = new PointerEventData(_eventSystem) { position = screen };
            List<RaycastResult> results = new List<RaycastResult>();
            _eventSystem.RaycastAll(pointer, results);

            Assert.IsNotEmpty(results, "RaycastArea 위치에 맞은 UI가 없음");
            Assert.AreSame(go, results[0].gameObject);
        }
    }
}
