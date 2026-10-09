using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // 점유 상태 관리와 스냅 오프셋 계산처럼 네 소켓(Chain/Inner/Value/Condition Out)이
    // 똑같이 갖고 있던 코드를 모았다. 구체 소켓 타입 이름은 그대로라 프리팹 참조에는 영향이 없다.
    /// <summary>
    /// 블록을 최대 1개 물고 있을 수 있는 연결부의 공통 기반이다.
    /// </summary>
    public abstract class BlockSocket : MonoBehaviour
    {
        private CodingBlock _occupant;

        public bool IsEmpty => !_occupant;
        public CodingBlock Occupant => _occupant;

        // 이 소켓이 딸린 블록 — 계층을 거슬러 찾는 대신 블록이 소켓을 등록할 때 지정한다
        public CodingBlock Owner { get; private set; }

        /// <summary>
        /// 소유 블록을 지정한다 (CodingBlock.RegisterSocket에서만 호출).
        /// </summary>
        public void SetOwner(CodingBlock owner) => Owner = owner;

        /// <summary>
        /// 블록을 이 소켓에 받아 붙인다 — 소켓 종류마다 스냅 위치와 원래 블록 처리 방식이 다르다.
        /// </summary>
        public abstract void Accept(CodingBlock block);

        /// <summary>
        /// 점유 블록 기록을 비운다.
        /// </summary>
        public virtual void Release() => _occupant = null;

        /// <summary>
        /// 점유 블록을 기록한다.
        /// </summary>
        protected void SetOccupant(CodingBlock block) => _occupant = block;

        /// <summary>
        /// 이름으로 직계 자식을 찾아 컴포넌트를 반환한다(자식이나 컴포넌트가 없으면 null).
        /// </summary>
        public static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            // 소켓 자식 이름은 BlockFactory가 Constants.Sockets 상수로 직접 만든 것이라 이름 탐색이 허용된다.
            Transform child = parent.Find(childName);
            if (child && child.TryGetComponent(out T component)) return component;
            return null;
        }

        /// <summary>
        /// 자식 블록의 In 소켓 앵커 위치가 이 소켓 위치와 겹치도록 anchoredPosition 오프셋을 계산한다.
        /// </summary>
        protected static Vector2 ComputeSnapOffset(CodingBlock block, Component inSocket)
        {
            if (!inSocket
                || !block.TryGetComponent(out RectTransform blockRt)
                || !inSocket.TryGetComponent(out RectTransform inRt))
                return Vector2.zero;

            Vector2 anchor = (inRt.anchorMin + inRt.anchorMax) * 0.5f;
            return -new Vector2(
                (anchor.x - blockRt.pivot.x) * blockRt.sizeDelta.x + inRt.anchoredPosition.x,
                (anchor.y - blockRt.pivot.y) * blockRt.sizeDelta.y + inRt.anchoredPosition.y);
        }

        /// <summary>
        /// 들어오는 블록의 체인·내부 소켓 어딘가에 Control 블록(시작하기/완성하기)이 섞여 있는지 확인한다 — 제어 블록은 FlowControl 내부로 들어갈 수 없다.
        /// </summary>
        protected static bool HasControlBlockInChain(CodingBlock block)
        {
            // 드래그 중 매 프레임 호출되므로 리스트를 만들지 않고 블록의 소켓 목록을 인덱스로 순회한다(재귀라 공용 버퍼도 쓸 수 없음)
            CodingBlock current = block;
            while (current)
            {
                if (current.Category == BlockCategory.Control) return true;

                IReadOnlyList<BlockSocket> sockets = current.Sockets;
                for (int i = 0; i < sockets.Count; i++)
                {
                    if (sockets[i] is InnerSocket innerSocket && innerSocket
                        && innerSocket.Occupant && HasControlBlockInChain(innerSocket.Occupant))
                        return true;
                }

                ChainOutSocket chainOut = ChainOutSocket.OfBlock(current);
                current = chainOut ? chainOut.Occupant : null;
            }
            return false;
        }

        /// <summary>
        /// 체인 소켓 전용 — 이미 점유 중인 소켓에 블록이 들어올 때, 밀려나는 블록(displaced)을
        /// 들어오는 체인의 꼬리에 이어 붙일 수 있는지 확인한다.
        /// </summary>
        protected static bool CanFit(CodingBlock incoming, CodingBlock displaced)
        {
            // 자기 자신을 밀어낼 수는 없다 — 받으면 블록이 자기 꼬리에 붙어 체인이 순환한다
            return !displaced || (displaced != incoming && FindChainTailOut(incoming));
        }

        /// <summary>
        /// 밀려난 블록(아래 체인째)을 들어온 체인의 꼬리에 이어 붙인다 — 붙일 곳이 없으면 코딩 존으로 옮긴다.
        /// </summary>
        protected static void AttachDisplacedToTail(CodingBlock incoming, CodingBlock displaced)
        {
            // 코딩 패널에 따로 놓아 둔 체인의 머리를 끌면 아래 블록을 단 채 들어오므로, 머리 바로 아래가 아니라 꼬리에 붙여야
            // 순서가 섞이지 않는다(A→B를 X→Y 사이에 넣으면 X→A→B→Y).
            if (!displaced || displaced == incoming) return;

            ChainOutSocket tailOut = FindChainTailOut(incoming);
            if (tailOut)
                tailOut.Accept(displaced);
            else
                displaced.MoveToCodingZone();
        }

        /// <summary>
        /// 체인 맨 끝 블록의 비어 있는 ChainOutSocket을 반환한다 — 끝 블록에 ChainOut이 없으면(완성하기 등) null.
        /// </summary>
        private static ChainOutSocket FindChainTailOut(CodingBlock head)
        {
            // 직속 소켓만 따라가 컨테이너 내부의 하위 체인 소켓과 혼동하지 않는다.
            ChainOutSocket tailOut = ChainOutSocket.OfBlock(head);
            while (tailOut && tailOut.Occupant)
                tailOut = ChainOutSocket.OfBlock(tailOut.Occupant);
            return tailOut;
        }
    }
}
