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
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out var block)) return;

            var all = new System.Collections.Generic.List<CodingBlock>();
            CollectAll(block, all);

            foreach (var b in all)
            {
                b.transform.SetParent(content);
                b.transform.SetAsLastSibling();
                b.SetHome(content);
            }
        }

        private void CollectAll(CodingBlock block, System.Collections.Generic.List<CodingBlock> all)
        {
            // 체인 자식을 먼저 분리 — 이후 GetComponentsInChildren이 손자 소켓을 잡지 않도록
            var chainOut = block.GetComponentInChildren<ChainOutSocket>();
            var chainChild = chainOut?.Occupant;
            if (chainOut) chainOut.Release();
            if (chainChild) chainChild.transform.SetParent(null, true);

            // 이 블록에 붙은 value 블록 분리 후 수집
            foreach (var vos in block.GetComponentsInChildren<ValueOutSocket>())
            {
                var valueBlock = vos.Occupant;
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
