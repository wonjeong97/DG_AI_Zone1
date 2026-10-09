using UnityEngine;

namespace Game
{
    // 블록 하단 연결 포인트. 자식 블록의 ChainInSocket과 위치를 맞춰 스냅한다.
    public class ChainOutSocket : BlockSocket
    {
        /// <summary>
        /// 블록의 '직속' ChainOutSocket을 반환한다 — 컨테이너(FlowControl/FuncDef) 내부의 하위 체인 소켓과 혼동하지 않는다.
        /// </summary>
        public static ChainOutSocket OfBlock(CodingBlock block)
        {
            return block ? block.GetSocket<ChainOutSocket>() : null;
        }

        /// <summary>
        /// 들어오는 블록을 받을 수 있는지(제어 블록 위치 제한, cascade 완료 가능 여부) 검증한다.
        /// </summary>
        public bool CanAccept(CodingBlock incoming)
        {
            if (incoming && incoming.Category == BlockCategory.Control)
            {
                // Inner 컨테이너(InnerSocket 하위) 내부에 위치한 소켓일 경우 Control 블록(완성하기 등) 수락 불가
                if (Owner && Owner.IsInsideInnerContainer())
                    return false;
            }

            return CanFit(incoming, Occupant);
        }

        /// <summary>
        /// 블록을 점유로 기록하고 스냅시키며, 원래 있던 블록은 들어온 체인의 꼬리에 이어 붙인다.
        /// </summary>
        public override void Accept(CodingBlock block)
        {
            CodingBlock displaced = Occupant;
            SetOccupant(block);
            block.SnapInto(transform, ComputeChainSnapOffset(block)).Forget();
            AttachDisplacedToTail(block, displaced);
        }

        /// <summary>
        /// 자식 블록의 ChainInSocket 앵커 위치가 이 소켓 위치와 일치하도록 오프셋을 계산한다.
        /// </summary>
        internal static Vector2 ComputeChainSnapOffset(CodingBlock block)
        {
            ChainInSocket inSocket = BlockSocket.FindChildComponent<ChainInSocket>(block.transform, Constants.Sockets.ChainInName);
            return ComputeSnapOffset(block, inSocket);
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        /// <summary>
        /// 씬 뷰에 소켓 위치와 스냅 범위를 표시한다.
        /// </summary>
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
