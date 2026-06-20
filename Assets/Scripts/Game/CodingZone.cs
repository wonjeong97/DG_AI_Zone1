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
            var block = e.pointerDrag?.GetComponent<CodingBlock>();
            if (!block || block.IsDragHandled) return;

            block.transform.SetParent(transform, true);
            block.SetHome(transform);

            if (block.Category == BlockCategory.Command)
                BlockFactory.AttachValueSlot(block.gameObject);
        }
    }
}
