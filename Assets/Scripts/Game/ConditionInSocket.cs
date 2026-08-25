using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game
{
    public class ConditionInSocket : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out RectTransform rt)) return;

            Gizmos.color = new Color(0.8f, 0.5f, 1f, 0.9f);
            float arm = 9f;
            var pos = (Vector2)rt.position;
            Gizmos.DrawLine(pos + Vector2.up * arm, pos + Vector2.down * arm);
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawWireSphere(rt.position, 3f);

            Handles.Label(rt.position + Vector3.down * 14f, "CondIn",
                new GUIStyle { normal = { textColor = new Color(0.8f, 0.5f, 1f) }, fontSize = 9 });
        }
#endif
    }
}
