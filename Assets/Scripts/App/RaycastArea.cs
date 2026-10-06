using UnityEngine;
using UnityEngine.UI;

namespace App
{
    // 화면에 아무것도 그리지 않고 터치(레이캐스트)만 받는 그래픽.
    // 알파 0 Image와 달리 정점을 내보내지 않아 오버드로우가 없고, MaskableGraphic이라 스크롤 뷰 마스크 밖에서는 터치를 받지 않는다.
    // Graphic 자체는 CanvasRenderer를 요구하지 않지만 GraphicRaycaster가 CanvasRenderer를 조회하므로 함께 붙인다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class RaycastArea : MaskableGraphic
    {
        /// <summary>
        /// 그릴 메시를 비워 레이캐스트 영역만 남긴다.
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
