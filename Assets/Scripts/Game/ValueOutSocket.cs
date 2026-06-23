using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    // Command 블록의 값 연결 포인트. Value 블록의 ValueInSocket과 위치를 맞춰 스냅한다.
    public class ValueOutSocket : MonoBehaviour
    {
        private CodingBlock _occupant;
        public bool IsEmpty => _occupant == null;
        public CodingBlock Occupant => _occupant;

        public void Accept(CodingBlock block)
        {
            _occupant = block;
            block.SnapInto(transform, ComputeSnapOffset(block)).Forget();
        }

        private static Vector2 ComputeSnapOffset(CodingBlock block)
        {
            var inSocket = block.GetComponentInChildren<ValueInSocket>();
            if (inSocket == null
                || !block.TryGetComponent<RectTransform>(out var blockRt)
                || !inSocket.TryGetComponent<RectTransform>(out var inRt))
                return Vector2.zero;

            var anchor = (inRt.anchorMin + inRt.anchorMax) * 0.5f;
            return -new Vector2(
                (anchor.x - blockRt.pivot.x) * blockRt.sizeDelta.x + inRt.anchoredPosition.x,
                (anchor.y - blockRt.pivot.y) * blockRt.sizeDelta.y + inRt.anchoredPosition.y);
        }

        public void Release() => _occupant = null;
        public void Reoccupy(CodingBlock block) => _occupant = block;

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out var rt)) return;

            var markerColor = IsEmpty ? new Color(1f, 0.85f, 0f, 0.9f)  : new Color(1f, 0.4f, 0.1f, 0.9f);
            var rangeColor  = IsEmpty ? new Color(1f, 0.85f, 0f, 0.08f) : new Color(1f, 0.4f, 0.1f, 0.08f);

            Gizmos.color = markerColor;
            float arm = 10f;
            var pos = (Vector2)rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos + Vector2.up   * arm, pos + Vector2.down  * arm);
            Gizmos.DrawWireSphere(rt.position, 4f);

            // 스냅 감지 범위 — 1·4사분면(우측 반원)
            Handles.color = rangeColor;
            Handles.DrawSolidArc(rt.position, Vector3.forward, Vector3.down, 180f, SnapRadius);
            Handles.color = markerColor;
            Handles.DrawWireArc(rt.position, Vector3.forward, Vector3.down, 180f, SnapRadius);

            Handles.Label(rt.position + Vector3.up * 16f, $"ValueOut  r={SnapRadius}",
                new GUIStyle { normal = { textColor = new Color(1f, 0.85f, 0f) }, fontSize = 9 });
        }
#endif
    }
}
