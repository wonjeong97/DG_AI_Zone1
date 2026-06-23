using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    // 블록 하단 연결 포인트. 자식 블록의 ChainInSocket과 위치를 맞춰 스냅한다.
    public class ChainOutSocket : MonoBehaviour
    {
        private CodingBlock _occupant;
        public bool IsEmpty => _occupant == null;
        public CodingBlock Occupant => _occupant;

        // cascade 전체가 완료될 수 있는지 재귀 검증
        public bool CanAccept(CodingBlock incoming) => CanFit(incoming, _occupant);

        private static bool CanFit(CodingBlock incoming, CodingBlock displaced)
        {
            if (displaced == null) return true;
            var nextOut = incoming.GetComponentInChildren<ChainOutSocket>();
            if (nextOut == null) return false;
            return CanFit(displaced, nextOut._occupant);
        }

        public void Accept(CodingBlock block)
        {
            var displaced = _occupant;
            _occupant = block;
            block.SnapInto(transform, ComputeSnapOffset(block)).Forget();

            if (displaced == null) return;

            // 드래그 시 splice-out으로 소켓이 비워졌으므로 최대 1단만 재귀됨
            var nextOut = block.GetComponentInChildren<ChainOutSocket>();
            if (nextOut == null)
            {
                // 안전망: 소켓 없는 블록(종료하기 등)이 들어온 경우 displaced를 CodingZone으로
                var zone = FindObjectOfType<CodingZone>();
                if (zone != null) { displaced.transform.SetParent(zone.transform, true); displaced.SetHome(zone.transform); }
                return;
            }
            nextOut.Accept(displaced);
        }

        // 자식 블록의 InSocket 앵커 위치가 이 소켓 위치와 일치하도록 오프셋 계산
        private static Vector2 ComputeSnapOffset(CodingBlock block)
        {
            var inSocket = block.transform.Find("ChainInSocket")?.GetComponent<ChainInSocket>();
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

            var markerColor = IsEmpty ? new Color(0f, 1f, 0.4f, 0.9f)  : new Color(1f, 0.3f, 0.3f, 0.9f);
            var rangeColor  = IsEmpty ? new Color(0f, 1f, 0.4f, 0.08f) : new Color(1f, 0.3f, 0.3f, 0.08f);

            // 십자 마커
            Gizmos.color = markerColor;
            float arm = 12f;
            var pos = (Vector2)rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos + Vector2.up   * arm, pos + Vector2.down  * arm);
            Gizmos.DrawWireSphere(rt.position, 5f);

            // 스냅 감지 범위 — 3·4사분면(하단 반원)
            Handles.color = rangeColor;
            Handles.DrawSolidArc(rt.position, Vector3.forward, Vector3.left, 180f, SnapRadius);
            Handles.color = markerColor;
            Handles.DrawWireArc(rt.position, Vector3.forward, Vector3.left, 180f, SnapRadius);

            Handles.Label(rt.position + Vector3.up * 18f, $"OutSocket  r={SnapRadius}",
                new GUIStyle { normal = { textColor = new Color(0f, 1f, 0.4f) }, fontSize = 9 });
        }
#endif
    }
}
