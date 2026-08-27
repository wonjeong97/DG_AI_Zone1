using UnityEngine;

namespace Game
{
    // 블록 상단 연결 포인트. 부모 블록의 ChainOutSocket 위치와 정렬되어 스냅된다.
    public class ChainInSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            var color = new Color(0.4f, 0.6f, 1f);
            SocketGizmos.DrawStem(rt, new Color(0.4f, 0.6f, 1f, 0.9f), arm: 10f, dotRadius: 4f, stemScale: 1.5f);
            SocketGizmos.DrawLabel(rt, "InSocket", color, yOffset: -14f);
        }
#endif
    }
}
