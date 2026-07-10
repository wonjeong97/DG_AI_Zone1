using UnityEngine;
using UnityEngine.EventSystems;

namespace DG.Game
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

            var all = new System.Collections.Generic.List<CodingBlock>();
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
                    b.gameObject.SetActive(b.Category == categoryZone.CurrentCategory);
                }
            }
        }

        private void CollectAll(CodingBlock block, System.Collections.Generic.List<CodingBlock> all)
        {
            // 체인 자식을 먼저 분리 — 이후 GetComponentsInChildren이 손자 소켓을 잡지 않도록
            ChainOutSocket chainOut = block.GetComponentInChildren<ChainOutSocket>();
            CodingBlock chainChild = chainOut?.Occupant;
            if (chainOut) chainOut.Release();
            if (chainChild) chainChild.transform.SetParent(null, true);

            // 이 블록에 붙은 value 블록 분리 후 수집
            foreach (ValueOutSocket vos in block.GetComponentsInChildren<ValueOutSocket>())
            {
                CodingBlock valueBlock = vos.Occupant;
                if (!valueBlock) continue;
                vos.Release();
                valueBlock.transform.SetParent(null, true);
                all.Add(valueBlock);
            }

            all.Add(block);

            if (chainChild) CollectAll(chainChild, all);
        }
    }
}
