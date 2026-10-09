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
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out CodingBlock block) || block.IsDragCancelled) return;

            // 붙을 소켓이 하이라이트돼 있으면 OnEndDrag가 그 소켓에 붙인다 — 여기서 먼저 옮기면 패널 배율로 크기가 바뀌어 하이라이트와 다르게 붙는다.
            // 핀치 취소 뒤 탭 전환으로 숨겨진 블록도 옮기지 않는다
            if (block.HasSnapTarget || !block.isActiveAndEnabled) return;

            // 드래그 시작이 조기 반환되면(루트 캔버스 없음) 블록이 아직 이전 소켓에 붙어 있으므로 해제
            Transform parent = block.transform.parent;
            if (parent && parent.TryGetComponent(out BlockSocket socket))
                socket.Release();

            block.transform.SetParent(transform, true);
            block.SetHome(transform);
            BlockFactory.AttachSockets(block);
        }
    }
}
