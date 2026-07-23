using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    // FlowControl 블록 내부 영역의 진입 소켓.
    // ChainOutSocket과 동일한 Accept/Release 인터페이스를 가지며,
    // CodingBlock의 스냅 탐색이 방향 제한 없이 이 소켓을 찾아 스냅한다.
    public class InnerSocket : MonoBehaviour
    {
        // 프리팹에서는 직렬화로 연결, 코드 생성 경로에서는 SetEmptyIndicator로 주입
        [SerializeField] private GameObject _emptyIndicator;

        private CodingBlock _occupant;

        public bool IsEmpty => !_occupant;
        public CodingBlock Occupant => _occupant;

        public void SetEmptyIndicator(GameObject go) => _emptyIndicator = go;

        public bool CanAccept(CodingBlock incoming) => CanFit(incoming, _occupant);

        private static bool CanFit(CodingBlock incoming, CodingBlock displaced)
        {
            if (!displaced) return true;
            ChainOutSocket nextOut = incoming.GetComponentInChildren<ChainOutSocket>();
            if (!nextOut) return false;
            return CanFit(displaced, nextOut.Occupant);
        }

        public void Accept(CodingBlock block)
        {
            CodingBlock displaced = _occupant;
            _occupant = block;
            block.SnapInto(transform, ComputeSnapOffset(block)).Forget();
            if (_emptyIndicator) _emptyIndicator.SetActive(false);

            if (!displaced) return;

            ChainOutSocket nextOut = block.GetComponentInChildren<ChainOutSocket>();
            if (nextOut)
            {
                nextOut.Accept(displaced);
            }
            else
            {
                CodingZone zone = FindObjectOfType<CodingZone>();
                if (zone) { displaced.transform.SetParent(zone.transform, true); displaced.SetHome(zone.transform); }
            }
        }

        public void Release()
        {
            _occupant = null;
            if (_emptyIndicator) _emptyIndicator.SetActive(true);
        }

        public void Reoccupy(CodingBlock block) => _occupant = block;

        private static Vector2 ComputeSnapOffset(CodingBlock block)
        {
            ChainInSocket inSocket = null;
            block.transform.Find(Constants.Sockets.ChainInName)?.TryGetComponent(out inSocket);
            if (!inSocket
                || !block.TryGetComponent<RectTransform>(out RectTransform blockRt)
                || !inSocket.TryGetComponent<RectTransform>(out RectTransform inRt))
                return Vector2.zero;

            var anchor = (inRt.anchorMin + inRt.anchorMax) * 0.5f;
            return -new Vector2(
                (anchor.x - blockRt.pivot.x) * blockRt.sizeDelta.x + inRt.anchoredPosition.x,
                (anchor.y - blockRt.pivot.y) * blockRt.sizeDelta.y + inRt.anchoredPosition.y);
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out RectTransform rt)) return;

            var markerColor = IsEmpty ? new Color(1f, 0.6f, 0f, 0.9f) : new Color(1f, 0.3f, 0.3f, 0.9f);
            var rangeColor  = IsEmpty ? new Color(1f, 0.6f, 0f, 0.08f) : new Color(1f, 0.3f, 0.3f, 0.08f);

            Gizmos.color = markerColor;
            float arm = 12f;
            var pos = (Vector2)rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos + Vector2.up   * arm, pos + Vector2.down  * arm);
            Gizmos.DrawWireSphere(rt.position, 5f);

            Handles.color = rangeColor;
            Handles.DrawSolidArc(rt.position, Vector3.forward, Vector3.left, 180f, SnapRadius);
            Handles.color = markerColor;
            Handles.DrawWireArc(rt.position, Vector3.forward, Vector3.left, 180f, SnapRadius);

            Handles.Label(rt.position + Vector3.up * 18f, $"InnerSocket  r={SnapRadius}",
                new GUIStyle { normal = { textColor = new Color(1f, 0.6f, 0f) }, fontSize = 9 });
        }
#endif
    }
}
