using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    // 블록 상단 연결 포인트. 부모 블록의 ChainOutSocket 위치와 정렬되어 스냅된다.
    public class ChainInSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out var rt)) return;

            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.9f);
            float arm = 10f;
            var pos = (Vector2)rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos, pos + Vector2.up * arm * 1.5f);
            Gizmos.DrawWireSphere(rt.position, 4f);

            Handles.Label(rt.position + Vector3.down * 14f, "InSocket",
                new GUIStyle { normal = { textColor = new Color(0.4f, 0.6f, 1f) }, fontSize = 9 });
        }
#endif
    }
}
