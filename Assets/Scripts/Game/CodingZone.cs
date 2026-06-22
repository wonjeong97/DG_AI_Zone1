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
            if (e.pointerDrag == null || !e.pointerDrag.TryGetComponent<CodingBlock>(out var block) || block.IsDragHandled) return;

            // ReturnHome이 블록을 임시로 이전 소켓/슬롯에 돌려놨을 수 있으므로 해제
            if (block.transform.parent.TryGetComponent<ChainOutSocket>(out var cs))
                cs.Release();
            else if (block.transform.parent.TryGetComponent<ValueOutSocket>(out var vos))
                vos.Release();

            block.transform.SetParent(transform, true);
            block.SetHome(transform);

            if (block.Category == BlockCategory.Command)
                BlockFactory.AttachValueOutSocket(block.gameObject, new Vector2(-8f, 11f));
            else if (block.Category == BlockCategory.Value)
                BlockFactory.AttachValueInSocket(block.gameObject, new Vector2(8f, 0f));

            var cat = block.Category;
            if (cat != BlockCategory.Value && cat != BlockCategory.Logic)
            {
                bool isStart   = block.gameObject.name == "시작하기";
                bool isEnd     = block.gameObject.name == "종료하기";
                bool isCommand = cat == BlockCategory.Command;

                var outOffset = isStart   ? new Vector2(-69f, 8f)  :
                                isCommand ? new Vector2(-78f, 4f)  : Vector2.zero;
                var inOffset  = isEnd     ? new Vector2(-73f, -20f) :
                                isCommand ? new Vector2(-78f, -16f) : Vector2.zero;

                if (!isEnd)
                    BlockFactory.AttachOutSocket(block.gameObject, outOffset);
                if (!isStart)
                    BlockFactory.AttachInSocket(block.gameObject, inOffset);
            }
        }
    }
}
