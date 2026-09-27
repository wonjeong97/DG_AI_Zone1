using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    // 코딩 패널 자유 배치 드랍존.
    // 드랍된 블록은 놓은 위치 그대로 유지된다.
    // 씬에서 생성된 블록 전체(인벤토리 대기 블록 포함) 목록도 여기서 관리해, 씬 전체를 뒤지지 않고 블록을 순회한다.
    public class CodingZone : MonoBehaviour, IDropHandler
    {
        [Tooltip("코딩 패널 스크롤뷰 — 블록 드롭 판정과 완성하기 초기 위치 보정에 뷰포트를 쓴다")]
        [SerializeField] private ScrollRect scrollRect;

        public ScrollRect ScrollRect => scrollRect;

        private readonly List<CodingBlock> _blocks = new();

        // BlockSpawner가 생성한 모든 블록 (코딩 패널 + 인벤토리, 비활성 탭 포함)
        public IReadOnlyList<CodingBlock> Blocks => _blocks;

        /// <summary>
        /// 새로 생성된 블록을 목록에 등록한다.
        /// </summary>
        public void RegisterBlock(CodingBlock block)
        {
            if (block && !_blocks.Contains(block)) _blocks.Add(block);
        }

        /// <summary>
        /// 블록이 현재 코딩 패널 안(소켓 하위 포함)에 놓여 있는지 확인한다.
        /// </summary>
        public bool Contains(CodingBlock block)
        {
            return block && block.transform.IsChildOf(transform);
        }

        /// <summary>
        /// 소켓에 붙지 않고 패널에 떨어진 블록을 놓은 위치 그대로 패널 직속으로 옮긴다.
        /// </summary>
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
