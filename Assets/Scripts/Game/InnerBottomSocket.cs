using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    // Inner 컨테이너 하단 기준점 — FlowInnerResize가 마지막 ChainOutSocket 위치와 맞춰 높이를 계산
    public class InnerBottomSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out RectTransform rt)) return;

            var pos = (Vector2)rt.position;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
            float arm = 12f;
            Gizmos.DrawLine(pos + Vector2.left  * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos + Vector2.up    * arm, pos + Vector2.down  * arm);
            Gizmos.DrawWireSphere(rt.position, 5f);

            Handles.Label(rt.position + Vector3.up * 18f, "InnerBottomSocket",
                new GUIStyle { normal = { textColor = new Color(0f, 0.8f, 1f) }, fontSize = 9 });
        }
#endif
    }
}
