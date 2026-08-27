using UnityEngine;

namespace Game
{
    // Inner 컨테이너 하단 기준점 — FlowInnerResize가 마지막 ChainOutSocket 위치와 맞춰 높이를 계산
    public class InnerBottomSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            var color = new Color(0f, 0.8f, 1f);
            SocketGizmos.DrawCross(rt, new Color(0f, 0.8f, 1f, 0.9f), arm: 12f, dotRadius: 5f);
            SocketGizmos.DrawLabel(rt, Constants.Sockets.InnerBottomName, color, yOffset: 18f);
        }
#endif
    }
}
