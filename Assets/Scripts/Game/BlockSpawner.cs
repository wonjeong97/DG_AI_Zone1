using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Game
{
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private Transform inventoryContainer;
        [SerializeField] private CodingZone codingZone;
        [SerializeField] private CategoryZone categoryZone;
        [Tooltip("블록 드래그 중 블록을 올려둘 최상위 캔버스")]
        [SerializeField] private Canvas rootCanvas;

        public CategoryZone CategoryZone => categoryZone;

        private IObjectResolver _resolver;
        private ILogger<BlockSpawner> _logger;

        /// <summary>
        /// 생성한 블록에 주입할 리졸버와 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<BlockSpawner> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        // VerticalLayoutGroup + ContentSizeFitter 체인이 한 프레임 만에 안정화되지 않아 필요한 리빌드 횟수
        private const int LayoutSettlePasses = 2;

        /// <summary>
        /// 레이아웃의 인벤토리 블록을 생성해 인벤토리(제어 블록은 코딩 패널)에 배치하고 카테고리 탭을 구성한다.
        /// 만드는 동안 인벤토리를 숨기고, 중간에 예외·취소로 빠져나가도 다시 보이게 한다.
        /// </summary>
        public async UniTask Spawn(BlockLayoutData layout, CancellationToken ct)
        {
            if (!HasDependencies()) return;

            // 스폰 및 카테고리 구성 중 인벤토리 깜빡임 방지를 위해 숨김 처리
            CanvasGroup invGroup = HideInventory();
            try
            {
                Clear(inventoryContainer);

                if (!layout || layout.inventoryBlocks is null)
                {
                    _logger.ZLogWarning($"[BlockSpawner] 레이아웃이 없어 블록을 생성하지 않습니다.");
                    return;
                }

                await CreateBlocksAsync(layout.inventoryBlocks, ct);

                // 모든 블록 생성 및 부모 지정 완료 후, UI 레이아웃과 텍스트 크기가 완전히 계산되도록 프레임 끝까지 대기 및 강제 리빌드
                // (VerticalLayoutGroup + ContentSizeFitter 체인이 한 프레임 만에 안정화되지 않아 여러 번 반복)
                for (int i = 0; i < LayoutSettlePasses; i++)
                    await SettleInventoryLayoutAsync(ct);

                if (categoryZone)
                    await categoryZone.Build(CollectInventoryCategories(), ct);

                // 카테고리 버튼 생성 및 첫 탭 선택/필터링이 완료된 후 최종 레이아웃을 정착시키고 표시
                await SettleInventoryLayoutAsync(ct);
            }
            finally
            {
                if (invGroup) invGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// 인벤토리 컨테이너를 투명하게 숨기고 그 CanvasGroup을 반환한다 (컨테이너가 없으면 null).
        /// </summary>
        private CanvasGroup HideInventory()
        {
            if (!inventoryContainer) return null;

            if (!inventoryContainer.TryGetComponent(out CanvasGroup invGroup))
                invGroup = inventoryContainer.gameObject.AddComponent<CanvasGroup>();
            invGroup.alpha = 0f;
            return invGroup;
        }

        /// <summary>
        /// 블록 항목을 차례로 만들어 인벤토리(제어 블록은 코딩 패널 고정 자리)에 놓는다.
        /// </summary>
        private async UniTask CreateBlocksAsync(BlockEntry[] entries, CancellationToken ct)
        {
            // 블록 프리팹은 리졸버로 인스턴스화되고, 코드로 덧붙이는 컴포넌트도 BlockFactory가 주입한다
            BlockFactory.BuildContext ctx = new BlockFactory.BuildContext(rootCanvas, _resolver, _logger);

            foreach (BlockEntry entry in entries)
            {
                GameObject go = await BlockFactory.Create(entry, ctx);
                // 어드레서블 로드를 기다리는 동안 씬을 떠났으면 더 만들지 않는다
                ct.ThrowIfCancellationRequested();
                RegisterSpawnedBlock(go);

                if (entry.category == BlockCategory.Control)
                    PlaceControlBlock(go, entry.controlRole == ControlRole.Start);
                else
                {
                    go.transform.SetParent(inventoryContainer, false);
                    if (go.TryGetComponent<CodingBlock>(out CodingBlock b))
                        b.SetInventoryHome(inventoryContainer);
                }
            }
        }

        /// <summary>
        /// 프레임 끝까지 기다린 뒤 캔버스와 인벤토리 레이아웃을 강제로 다시 계산한다.
        /// </summary>
        private async UniTask SettleInventoryLayoutAsync(CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            Canvas.ForceUpdateCanvases();
            if (inventoryContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        /// <summary>
        /// 주입이 끝났는지 확인하고, 블록 동작에 필요한 인스펙터 참조가 빠져 있으면 경고를 남긴다.
        /// </summary>
        private bool HasDependencies()
        {
            if (_logger == null || _resolver == null)
            {
                Debug.LogError("[BlockSpawner] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");
                return false;
            }

            if (!inventoryContainer) _logger.ZLogWarning($"[BlockSpawner] inventoryContainer가 연결되지 않아 인벤토리 블록을 놓을 곳이 없습니다.");
            if (!rootCanvas) _logger.ZLogWarning($"[BlockSpawner] rootCanvas가 연결되지 않아 블록 드래그가 동작하지 않습니다.");
            if (!codingZone) _logger.ZLogWarning($"[BlockSpawner] codingZone이 연결되지 않아 블록 스냅·복귀가 동작하지 않습니다.");
            if (!categoryZone) _logger.ZLogWarning($"[BlockSpawner] categoryZone이 연결되지 않아 카테고리 탭을 만들지 않습니다.");
            return true;
        }

        /// <summary>
        /// 생성된 블록에 존 참조를 넘기고 코딩 패널의 블록 목록에 등록한다.
        /// </summary>
        private void RegisterSpawnedBlock(GameObject go)
        {
            if (!go || !go.TryGetComponent<CodingBlock>(out CodingBlock block))
            {
                if (_logger != null) _logger.ZLogWarning($"[BlockSpawner] 생성된 오브젝트에 CodingBlock이 없어 등록하지 않습니다.");
                return;
            }

            block.SetZones(codingZone, categoryZone);
            if (codingZone) codingZone.RegisterBlock(block);
        }

        /// <summary>
        /// 인벤토리에 스폰된 블록들의 탭 카테고리를 등장 순서대로(중복 없이) 수집한다.
        /// </summary>
        private List<BlockCategory> CollectInventoryCategories()
        {
            List<BlockCategory> categories = new List<BlockCategory>();
            if (!inventoryContainer) return categories;

            foreach (Transform child in inventoryContainer)
            {
                if (!child.TryGetComponent<CodingBlock>(out CodingBlock block)) continue;

                BlockCategory tab = BlockFactory.GetTabCategory(block.Category);
                if (!categories.Contains(tab)) categories.Add(tab);
            }
            return categories;
        }

        /// <summary>
        /// 시작하기/완성하기는 인벤토리 대신 코딩 패널에 초기 배치한다 (시작: 좌상단, 완성: 좌하단).
        /// </summary>
        private void PlaceControlBlock(GameObject go, bool isStart)
        {
            if (!codingZone) return;

            go.transform.SetParent(codingZone.transform, false);

            CodingBlock.ApplyControlBlockLayout(go.transform as RectTransform, isStart, codingZone);

            if (go.TryGetComponent<CodingBlock>(out CodingBlock block))
            {
                block.SetHome(codingZone.transform);
                BlockFactory.AttachSockets(block);
            }
        }

        /// <summary>
        /// 컨테이너의 기존 자식을 모두 파괴한다.
        /// </summary>
        private static void Clear(Transform t)
        {
            if (!t) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
