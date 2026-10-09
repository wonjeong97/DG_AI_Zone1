using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 레이캐스트 테스트에 쓰는 EventSystem과 화면 오버레이 캔버스 묶음.
    /// 여러 테스트가 같은 준비·정리 코드를 따로 갖고 있던 것을 모았다.
    /// </summary>
    internal sealed class UiRaycastStage
    {
        public readonly EventSystem EventSystem;
        public readonly Canvas Canvas;

        private readonly GameObject _eventSystemGo;
        private readonly GameObject _canvasGo;

        /// <summary>
        /// 이름 앞에 테스트 이름을 붙여 EventSystem과 GraphicRaycaster가 달린 오버레이 캔버스를 만든다.
        /// </summary>
        public UiRaycastStage(string testName)
        {
            _eventSystemGo = new GameObject(testName + "_EventSystem", typeof(EventSystem));
            _eventSystemGo.TryGetComponent(out EventSystem);
            _canvasGo = new GameObject(testName + "_Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _canvasGo.TryGetComponent(out Canvas);
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>
        /// 캔버스와 EventSystem을 즉시 파괴한다 — 프레임 끝까지 남으면 다음 테스트의 EventSystem.current·레이캐스트와 겹친다.
        /// </summary>
        public void Destroy()
        {
            if (_canvasGo) Object.DestroyImmediate(_canvasGo);
            if (_eventSystemGo) Object.DestroyImmediate(_eventSystemGo);
        }
    }
}
