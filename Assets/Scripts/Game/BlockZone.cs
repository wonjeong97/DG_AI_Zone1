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
            var block = e.pointerDrag?.GetComponent<CodingBlock>();
            if (block == null) return;

            block.transform.SetParent(content);
            block.transform.SetAsLastSibling();
            block.SetHome(content);
        }
    }
}
