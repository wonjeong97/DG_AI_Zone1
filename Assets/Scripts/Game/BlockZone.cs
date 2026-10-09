using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using ZLogger;

namespace Game
{
    // 인벤토리 / 코딩 패널 공통 드랍 존.
    // 드랍된 블록은 content 컨테이너 맨 아래에 추가된다.
    public class BlockZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Transform content;

        private ILogger<BlockZone> _logger;

        /// <summary>
        /// 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<BlockZone> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 드롭된 블록과 거기 붙어 있던 블록 전부를 떼어 content 맨 아래에 차례로 넣는다.
        /// </summary>
        public void OnDrop(PointerEventData e)
        {
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out CodingBlock block)) return;

            // 핀치로 취소된 드래그는 이미 제자리로 돌아갔다
            if (block.IsDragCancelled) return;

            // 같은 블록을 함께 누르고 있던 다른 손가락이 놓은 것이면 옮기지 않는다
            if (!block.IsDragPointer(e)) return;

            // 붙을 소켓이 하이라이트돼 있으면 OnEndDrag가 그 소켓에 붙인다 — 여기서 먼저 옮기면 붙어 있던 블록만 목록으로 갈라진다.
            // 핀치 취소 뒤 탭 전환으로 숨겨진 블록도 옮기지 않는다
            if (block.HasSnapTarget || !block.isActiveAndEnabled) return;

            // 시작하기/완성하기는 코딩 패널 전용 — 인벤토리 반입 금지 (거부 시 원래 자리로 복귀)
            if (block.Category == BlockCategory.Control) return;

            List<CodingBlock> all = new List<CodingBlock>();
            CollectAll(block, all);

            // 존 참조는 드롭된 블록이 생성 시 BlockSpawner에게서 받아 들고 있다
            CategoryZone categoryZone = block.CategoryZone;

            foreach (CodingBlock b in all)
            {
                b.transform.SetParent(content);
                b.transform.SetAsLastSibling();
                b.SetHome(content);

                // 인벤토리로 반입될 때 현재 선택된 카테고리와 다른 경우 비활성화 처리
                if (categoryZone && content == categoryZone.InventoryContent)
                {
                    b.gameObject.SetActive(BlockFactory.GetTabCategory(b.Category) == categoryZone.CurrentCategory);
                }
            }
        }

        /// <summary>
        /// 블록에 붙은 내부·값·조건·체인 블록을 모두 떼어내며 하위까지 재귀 수집한다 (제어 블록은 제자리로 복귀).
        /// </summary>
        private void CollectAll(CodingBlock block, List<CodingBlock> all)
        {
            if (block.Category == BlockCategory.Control)
            {
                CodingZone codingZone = block.CodingZone;
                if (codingZone)
                    CodingBlock.ResetControlBlockPosition(block, codingZone);
                else if (_logger != null)
                    _logger.ZLogWarning($"[BlockZone] {block.name}에 CodingZone이 연결되지 않아 제자리로 되돌릴 수 없습니다.");
                return;
            }

            // 1. InnerSocket (FlowControl 내부 컨테이너에 들어간 블록) 분리 후 수집
            DetachAndCollect<InnerSocket>(block, all);

            // 2. 체인으로 이어진 다음 블록 분리 (수집은 이 블록을 넣은 뒤 — 순서 유지)
            ChainOutSocket chainOut = ChainOutSocket.OfBlock(block);
            CodingBlock chainChild = chainOut ? chainOut.Occupant : null;
            if (chainOut) chainOut.Release();
            if (chainChild) chainChild.transform.SetParent(null, true);

            // 3. 이 블록에 붙은 value 블록 분리 후 수집
            DetachAndCollect<ValueOutSocket>(block, all);

            // 4. 이 블록에 붙은 condition 블록 분리 후 수집
            DetachAndCollect<ConditionOutSocket>(block, all);

            all.Add(block);

            if (chainChild) CollectAll(chainChild, all);
        }

        /// <summary>
        /// 블록에 직접 딸린 지정 종류의 소켓에 물려 있는 블록을 모두 떼어내고 그 하위까지 재귀 수집한다.
        /// </summary>
        private void DetachAndCollect<TSocket>(CodingBlock block, List<CodingBlock> all)
            where TSocket : BlockSocket
        {
            List<TSocket> sockets = new List<TSocket>();
            block.GetSockets(sockets);

            foreach (TSocket socket in sockets)
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
