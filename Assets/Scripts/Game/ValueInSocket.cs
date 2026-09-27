using UnityEngine;

namespace Game
{
    // Value 블록의 연결 포인트. 부모 Command 블록의 ValueOutSocket 위치와 정렬되어 스냅된다.
    public class ValueInSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에 소켓 위치와 이름을 표시한다.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            Color color = new Color(1f, 0.6f, 0.1f);
            SocketGizmos.DrawCross(rt, new Color(1f, 0.6f, 0.1f, 0.9f), arm: 9f, dotRadius: 3f);
            SocketGizmos.DrawLabel(rt, "ValueIn", color, yOffset: -14f);
        }
#endif
    }
}
