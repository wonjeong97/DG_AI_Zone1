using UnityEngine;
using UnityEngine.EventSystems;

namespace DG.Game
{
    // 코딩 패널 자유 배치 드랍존.
    // 드랍된 블록은 놓은 위치 그대로 유지된다.
    public class CodingZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData e)
        {
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out CodingBlock block) || block.IsDragHandled) return;

            // ReturnHome이 블록을 임시로 이전 소켓/슬롯에 돌려놨을 수 있으므로 해제
            if (block.transform.parent.TryGetComponent<ChainOutSocket>(out ChainOutSocket cs))
                cs.Release();
            else if (block.transform.parent.TryGetComponent<ValueOutSocket>(out ValueOutSocket vos))
                vos.Release();

            block.transform.SetParent(transform, true);
            block.SetHome(transform);
            BlockFactory.AttachSockets(block);
        }
    }
}
