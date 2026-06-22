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
            if (e.pointerDrag == null || !e.pointerDrag.TryGetComponent<CodingBlock>(out var block)) return;

            block.transform.SetParent(content);
            block.transform.SetAsLastSibling();
            block.SetHome(content);
        }
    }
}
