using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    // 인벤토리 / 코딩 패널 공통 드랍 존.
    // 드랍된 블록은 content 컨테이너 맨 아래에 추가된다.
    public class BlockZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Transform content;

        public Transform Content => content;

        public void OnDrop(PointerEventData e)
        {
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out CodingBlock block)) return;

            // 시작하기/완성하기는 코딩 패널 전용 — 인벤토리 반입 금지 (거부 시 원래 자리로 복귀)
            if (block.Category == BlockCategory.Control) return;

            var all = new List<CodingBlock>();
            CollectAll(block, all);

            CategoryZone categoryZone = FindObjectOfType<CategoryZone>();

            foreach (CodingBlock b in all)
            {
                b.transform.SetParent(content);
                b.transform.SetAsLastSibling();
                b.SetHome(content);

                // 인벤토리로 반입될 때 현재 선택된 카테고리와 다른 경우 비활성화 처리
                if (categoryZone != null && content == categoryZone.InventoryContent)
                {
                    b.gameObject.SetActive(BlockFactory.GetTabCategory(b.Category) == categoryZone.CurrentCategory);
                }
            }
        }

        private void CollectAll(CodingBlock block, List<CodingBlock> all)
        {
            if (block.Category == BlockCategory.Control)
            {
                CodingZone codingZone = FindObjectOfType<CodingZone>();
                if (codingZone)
                {
                    CodingBlock.ResetControlBlockPosition(block, codingZone.transform);
                }
                return;
            }

            // 1. InnerSocket (FlowControl 내부 컨테이너에 들어간 블록) 분리 후 수집
            DetachAndCollect<InnerSocket>(block, all, includeInactive: true);

            // 2. ChainOutSocket 자식을 먼저 분리 — 이후 GetComponentsInChildren이 손자 소켓을 잡지 않도록
            ChainOutSocket chainOut = block.GetComponentInChildren<ChainOutSocket>();
            CodingBlock chainChild = chainOut?.Occupant;
            if (chainOut) chainOut.Release();
            if (chainChild) chainChild.transform.SetParent(null, true);

            // 3. 이 블록에 붙은 value 블록 분리 후 수집
            DetachAndCollect<ValueOutSocket>(block, all, includeInactive: false);

            // 4. 이 블록에 붙은 condition 블록 분리 후 수집
            DetachAndCollect<ConditionOutSocket>(block, all, includeInactive: false);

            all.Add(block);

            if (chainChild) CollectAll(chainChild, all);
        }

        // 지정한 종류의 소켓에 물려 있는 블록을 모두 떼어내고 그 하위까지 재귀 수집한다.
        private void DetachAndCollect<TSocket>(CodingBlock block, List<CodingBlock> all, bool includeInactive)
            where TSocket : BlockSocket
        {
            foreach (TSocket socket in block.GetComponentsInChildren<TSocket>(includeInactive))
            {
                CodingBlock child = socket.Occupant;
                if (!child) continue;

                socket.Release();
                child.transform.SetParent(null, true);
                CollectAll(child, all);
            }
        }
    }
}
