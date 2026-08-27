using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // FlowControl 블록 내부 영역의 진입 소켓.
    // ChainOutSocket과 동일한 Accept/Release 인터페이스를 가지며,
    // CodingBlock의 스냅 탐색이 방향 제한 없이 이 소켓을 찾아 스냅한다.
    public class InnerSocket : BlockSocket
    {
        // 프리팹에서는 직렬화로 연결, 코드 생성 경로에서는 SetEmptyIndicator로 주입
        [SerializeField] private GameObject _emptyIndicator;

        public void SetEmptyIndicator(GameObject go) => _emptyIndicator = go;

        public bool CanAccept(CodingBlock incoming)
        {
            if (incoming != null && HasControlBlockInChain(incoming))
                return false;
            return CanFit(incoming, Occupant);
        }

        // 들어오는 블록의 체인·내부 소켓 어딘가에 Control 블록(시작하기/완성하기)이 섞여 있는지 확인.
        // 제어 블록은 FlowControl 내부로 들어갈 수 없다.
        private static bool HasControlBlockInChain(CodingBlock block)
        {
            CodingBlock current = block;
            while (current)
            {
                if (current.Category == BlockCategory.Control) return true;

                foreach (InnerSocket innerSocket in current.GetComponentsInChildren<InnerSocket>(true))
                {
                    if (innerSocket.Occupant && HasControlBlockInChain(innerSocket.Occupant))
                        return true;
                }

                ChainOutSocket chainOut = current.GetComponentInChildren<ChainOutSocket>();
                current = chainOut ? chainOut.Occupant : null;
            }
            return false;
        }

        public void Accept(CodingBlock block)
        {
            // 블록이 내부 소켓에 결합되는 즉시 상위 블록 하이라이트 확실히 해제
            CodingBlock parentBlock = GetComponentInParent<CodingBlock>();
            if (parentBlock) parentBlock.ClearSnapHighlight();

            CodingBlock displaced = Occupant;
            SetOccupant(block);
            block.SnapInto(transform, ChainOutSocket.ComputeChainSnapOffset(block)).Forget();
            if (_emptyIndicator) _emptyIndicator.SetActive(false);

            if (!displaced) return;

            // 직속 소켓만 사용 — 컨테이너 내부 하위 소켓을 잡아 치환 블록이 잘못 들어가는 문제 방지
            ChainOutSocket nextOut = ChainOutSocket.OfBlock(block);
            if (nextOut)
                nextOut.Accept(displaced);
            else
                MoveToCodingZone(displaced);
        }

        public override void Release()
        {
            base.Release();
            if (_emptyIndicator) _emptyIndicator.SetActive(true);
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            Color markerColor = IsEmpty ? new Color(1f, 0.6f, 0f, 0.9f)  : new Color(1f, 0.3f, 0.3f, 0.9f);
            Color rangeColor  = IsEmpty ? new Color(1f, 0.6f, 0f, 0.08f) : new Color(1f, 0.3f, 0.3f, 0.08f);

            SocketGizmos.DrawCross(rt, markerColor, arm: 12f, dotRadius: 5f);
            SocketGizmos.DrawSnapRange(rt, Vector3.left, SnapRadius, markerColor, rangeColor);
            SocketGizmos.DrawLabel(rt, $"InnerSocket  r={SnapRadius}", new Color(1f, 0.6f, 0f), yOffset: 18f);
        }
#endif
    }
}
