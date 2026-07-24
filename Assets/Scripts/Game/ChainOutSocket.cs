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
        public bool IsEmpty => !_occupant;
        public CodingBlock Occupant => _occupant;

        // 블록의 '직속' ChainOutSocket — 컨테이너(FlowControl/FuncDef) 내부의 하위 체인 소켓과 혼동 방지
        public static ChainOutSocket OfBlock(CodingBlock block)
        {
            ChainOutSocket socket = null;
            block.transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out socket);
            return socket;
        }

        // cascade 전체가 완료될 수 있는지 재귀 검증
        public bool CanAccept(CodingBlock incoming)
        {
            if (incoming != null && incoming.Category == BlockCategory.Control)
            {
                // Inner 컨테이너(InnerSocket 하위) 내부에 위치한 소켓일 경우 Control 블록(완성하기 등) 수락 불가
                if (GetComponentInParent<InnerSocket>() != null)
                    return false;
            }

            // 레벨 5(함수) 한정 — 메인 체인(시작~완성)에는 함수 블록만 연결 가능
            if (CodingBlock.RestrictMainChainToFunction && incoming != null)
            {
                CodingBlock owner = GetComponentInParent<CodingBlock>();
                if (owner)
                {
                    bool ownerIsStart   = owner.Category == BlockCategory.Control
                                          && owner.ControlRole == DG.Data.ControlRole.Start;
                    bool ownerIsFunc    = owner.Category == BlockCategory.Function;
                    bool incomingIsFunc = incoming.Category == BlockCategory.Function;
                    bool incomingIsEnd  = incoming.Category == BlockCategory.Control
                                          && incoming.ControlRole == DG.Data.ControlRole.End;

                    // 시작하기 소켓엔 함수만 / 함수 소켓엔 완성하기만(삽입 금지) / 완성하기는 함수 뒤에만
                    if (ownerIsStart && !incomingIsFunc) return false;
                    if (ownerIsFunc && !incomingIsEnd) return false;
                    if (incomingIsEnd && !ownerIsFunc) return false;
                }
            }

            return CanFit(incoming, _occupant);
        }

        private static bool CanFit(CodingBlock incoming, CodingBlock displaced)
        {
            if (!displaced) return true;
            ChainOutSocket nextOut = OfBlock(incoming);
            if (!nextOut) return false;
            return CanFit(displaced, nextOut._occupant);
        }

        public void Accept(CodingBlock block)
        {
            CodingBlock displaced = _occupant;
            _occupant = block;
            block.SnapInto(transform, ComputeSnapOffset(block)).Forget();

            if (!displaced) return;

            // 드래그 시 splice-out으로 소켓이 비워졌으므로 최대 1단만 재귀됨.
            // 직속 소켓만 사용 — 컨테이너 내부의 하위 소켓을 잡아 치환 블록이 안으로 들어가는 문제 방지
            ChainOutSocket nextOut = OfBlock(block);
            if (!nextOut)
            {
                // 안전망: 소켓 없는 블록(완성하기 등)이 들어온 경우 displaced를 CodingZone으로
                CodingZone zone = FindObjectOfType<CodingZone>();
                if (zone) { displaced.transform.SetParent(zone.transform, true); displaced.SetHome(zone.transform); }
                return;
            }
            nextOut.Accept(displaced);
        }

        // 자식 블록의 InSocket 앵커 위치가 이 소켓 위치와 일치하도록 오프셋 계산
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

        public void Release() => _occupant = null;
        public void Reoccupy(CodingBlock block) => _occupant = block;

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent<RectTransform>(out RectTransform rt)) return;

            Color markerColor = IsEmpty ? new Color(0f, 1f, 0.4f, 0.9f)  : new Color(1f, 0.3f, 0.3f, 0.9f);
            Color rangeColor  = IsEmpty ? new Color(0f, 1f, 0.4f, 0.08f) : new Color(1f, 0.3f, 0.3f, 0.08f);

            // 십자 마커
            Gizmos.color = markerColor;
            float arm = 12f;
            Vector2 pos = (Vector2)rt.position;
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
