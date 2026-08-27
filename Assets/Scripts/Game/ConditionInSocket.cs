using UnityEngine;

namespace Game
{
    // 조건/Logic 블록의 좌측 연결 포인트. 앞 블록의 ConditionOutSocket 위치와 정렬되어 스냅된다.
    public class ConditionInSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            var color = new Color(0.8f, 0.5f, 1f);
            SocketGizmos.DrawCross(rt, new Color(0.8f, 0.5f, 1f, 0.9f), arm: 9f, dotRadius: 3f);
            SocketGizmos.DrawLabel(rt, "CondIn", color, yOffset: -14f);
        }
#endif
    }
}
