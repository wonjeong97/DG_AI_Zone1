using UnityEngine;

namespace Game
{
    // 블록 하단 연결 포인트. 자식 블록의 ChainInSocket과 위치를 맞춰 스냅한다.
    public class ChainOutSocket : BlockSocket
    {
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
                                          && owner.ControlRole == Data.ControlRole.Start;
                    bool ownerIsFunc    = owner.Category == BlockCategory.Function;
                    bool incomingIsFunc = incoming.Category == BlockCategory.Function;
                    bool incomingIsEnd  = incoming.Category == BlockCategory.Control
                                          && incoming.ControlRole == Data.ControlRole.End;

                    // 시작하기 소켓엔 함수만 / 함수 소켓엔 완성하기만(삽입 금지) / 완성하기는 함수 뒤에만
                    if (ownerIsStart && !incomingIsFunc) return false;
                    if (ownerIsFunc && !incomingIsEnd) return false;
                    if (incomingIsEnd && !ownerIsFunc) return false;
                }
            }

            return CanFit(incoming, Occupant);
        }

        public void Accept(CodingBlock block)
        {
            CodingBlock displaced = Occupant;
            SetOccupant(block);
            block.SnapInto(transform, ComputeChainSnapOffset(block)).Forget();

            if (!displaced) return;

            // 드래그 시 splice-out으로 소켓이 비워졌으므로 최대 1단만 재귀됨.
            // 직속 소켓만 사용 — 컨테이너 내부의 하위 소켓을 잡아 치환 블록이 안으로 들어가는 문제 방지
            ChainOutSocket nextOut = OfBlock(block);
            if (nextOut)
                nextOut.Accept(displaced);
            else
                MoveToCodingZone(displaced);
        }

        // 자식 블록의 ChainInSocket 앵커 위치가 이 소켓 위치와 일치하도록 오프셋 계산
        internal static Vector2 ComputeChainSnapOffset(CodingBlock block)
        {
            ChainInSocket inSocket = null;
            block.transform.Find(Constants.Sockets.ChainInName)?.TryGetComponent(out inSocket);
            return ComputeSnapOffset(block, inSocket);
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            Color markerColor = IsEmpty ? new Color(0f, 1f, 0.4f, 0.9f)  : new Color(1f, 0.3f, 0.3f, 0.9f);
            Color rangeColor  = IsEmpty ? new Color(0f, 1f, 0.4f, 0.08f) : new Color(1f, 0.3f, 0.3f, 0.08f);

            SocketGizmos.DrawCross(rt, markerColor, arm: 12f, dotRadius: 5f);
            // 스냅 감지 범위 — 3·4사분면(하단 반원)
            SocketGizmos.DrawSnapRange(rt, Vector3.left, SnapRadius, markerColor, rangeColor);
            SocketGizmos.DrawLabel(rt, $"OutSocket  r={SnapRadius}", new Color(0f, 1f, 0.4f), yOffset: 18f);
        }
#endif
    }
}
