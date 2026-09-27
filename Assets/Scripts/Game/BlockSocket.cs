using UnityEngine;

namespace Game
{
    /// <summary>
    /// 블록을 최대 1개 물고 있을 수 있는 연결부의 공통 기반.
    /// 점유 상태 관리와 스냅 오프셋 계산처럼 네 소켓(Chain/Inner/Value/Condition Out)이
    /// 똑같이 갖고 있던 코드를 모았다. 구체 소켓 타입 이름은 그대로라 프리팹 참조에는 영향이 없다.
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
        /// 점유 블록 기록을 비운다.
        /// </summary>
        public virtual void Release() => _occupant = null;

        /// <summary>
        /// 드래그가 취소되어 블록이 원래 소켓으로 되돌아온 경우 점유 상태만 복구한다.
        /// </summary>
        public void Reoccupy(CodingBlock block) => _occupant = block;

        /// <summary>
        /// 점유 블록을 기록한다.
        /// </summary>
        protected void SetOccupant(CodingBlock block) => _occupant = block;

        /// <summary>
        /// 이름으로 직계 자식을 찾아 컴포넌트를 반환한다. 자식이나 컴포넌트가 없으면 null.
        /// 소켓 자식 이름은 BlockFactory가 Constants.Sockets 상수로 직접 만든 것이라 이름 탐색이 허용된다.
        /// </summary>
        public static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
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
        /// 체인 소켓 전용 — 이미 점유 중인 소켓에 블록이 들어올 때, 밀려나는 블록(displaced)이
        /// cascade 끝까지 자리를 찾을 수 있는지 재귀 검증한다.
        /// </summary>
        protected static bool CanFit(CodingBlock incoming, CodingBlock displaced)
        {
            if (!displaced) return true;

            ChainOutSocket nextOut = ChainOutSocket.OfBlock(incoming);
            if (!nextOut) return false;

            return CanFit(displaced, nextOut.Occupant);
        }

        /// <summary>
        /// 안전망 — 소켓 없는 블록(완성하기 등)이 들어와 밀려난 블록을 넘길 곳이 없을 때
        /// 코딩존 직속으로 되돌린다.
        /// </summary>
        protected static void MoveToCodingZone(CodingBlock block)
        {
            CodingZone zone = block.CodingZone;
            if (!zone)
            {
                Debug.LogWarning($"[BlockSocket] {block.name}에 CodingZone이 연결되지 않아 코딩 패널로 옮기지 못했습니다.");
                return;
            }

            block.transform.SetParent(zone.transform, true);
            block.SetHome(zone.transform);
        }
    }
}
