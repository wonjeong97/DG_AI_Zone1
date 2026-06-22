using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    // Value 블록의 연결 포인트. 부모 Command 블록의 ValueOutSocket 위치와 정렬되어 스냅된다.
    public class ValueInSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out var rt)) return;

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            float arm = 9f;
            var pos = (Vector2)rt.position;
            Gizmos.DrawLine(pos + Vector2.up * arm, pos + Vector2.down * arm);
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawWireSphere(rt.position, 3f);

            Handles.Label(rt.position + Vector3.down * 14f, "ValueIn",
                new GUIStyle { normal = { textColor = new Color(1f, 0.6f, 0.1f) }, fontSize = 9 });
        }
#endif
    }
}
