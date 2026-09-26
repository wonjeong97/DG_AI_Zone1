using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace Game
{
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private Transform inventoryContainer;
        [SerializeField] private Transform codingContainer;
        [SerializeField] private CategoryZone categoryZone;

        public CategoryZone CategoryZone => categoryZone;

        private IObjectResolver _resolver;
        private ILogger<BlockSpawner> _log;

        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<BlockSpawner> log)
        {
            _resolver = resolver;
            _log = log;
        }

        // VerticalLayoutGroup + ContentSizeFitter 체인이 한 프레임 만에 안정화되지 않아 필요한 리빌드 횟수
        private const int LayoutSettlePasses = 2;

        private Canvas _rootCanvas;
        private CodingZone _codingZone;

        private void Awake()
        {
            _rootCanvas = RootCanvasOf(inventoryContainer);
            if (!_rootCanvas) _rootCanvas = RootCanvasOf(transform);

            // codingContainer는 CodingZone이 붙은 코딩 패널 Content — 생성한 블록에 넘겨줄 참조를 여기서 확보
            if (codingContainer) codingContainer.TryGetComponent(out _codingZone);
        }

        // 부모 계층의 Canvas에서 루트 캔버스를 얻는다 (없으면 null)
        private static Canvas RootCanvasOf(Transform t)
        {
            if (!t) return null;
            Canvas canvas = t.GetComponentInParent<Canvas>();
            return canvas ? canvas.rootCanvas : null;
        }

        public async UniTask Spawn(BlockLayoutData layout)
        {
            // 스폰 및 카테고리 구성 중 인벤토리 깜빡임 방지를 위해 숨김 처리
            CanvasGroup invGroup = null;
            if (inventoryContainer)
            {
                if (!inventoryContainer.TryGetComponent(out invGroup))
                    invGroup = inventoryContainer.gameObject.AddComponent<CanvasGroup>();
                invGroup.alpha = 0f;
            }

            Clear(inventoryContainer);

            if (!_rootCanvas && _log != null)
                _log.ZLogWarning($"[BlockSpawner] inventoryContainer 상위에 Canvas가 없어 블록 드래그가 동작하지 않습니다.");
            if (!_codingZone && _log != null)
                _log.ZLogWarning($"[BlockSpawner] codingContainer에 CodingZone이 없어 블록 스냅·복귀가 동작하지 않습니다.");

            if (!layout || layout.inventoryBlocks is null)
            {
                if (invGroup) invGroup.alpha = 1f;
                return;
            }

            foreach (var entry in layout.inventoryBlocks)
            {
                GameObject go = await BlockFactory.Create(entry, _rootCanvas, draggable: true);
                if (go.TryGetComponent<CodingBlock>(out CodingBlock created))
                    created.SetZones(_codingZone, categoryZone);
                if (entry.category == BlockCategory.Control)
                    PlaceControlBlock(go, entry.controlRole == ControlRole.Start);
                else
                {
                    go.transform.SetParent(inventoryContainer, false);
                    if (go.TryGetComponent<CodingBlock>(out CodingBlock b))
                        b.SetInventoryHome(inventoryContainer);
                }
                _resolver?.InjectGameObject(go);
            }

            // 모든 블록 생성 및 부모 지정 완료 후, UI 레이아웃과 텍스트 크기가 완전히 계산되도록 프레임 끝까지 대기 및 강제 리빌드
            // (VerticalLayoutGroup + ContentSizeFitter 체인이 한 프레임 만에 안정화되지 않아 2회 반복)
            for (int i = 0; i < LayoutSettlePasses; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Canvas.ForceUpdateCanvases();
                if (inventoryContainer is RectTransform rt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }

            if (categoryZone)
                await categoryZone.Build(CollectInventoryCategories());

            // 카테고리 버튼 생성 및 첫 탭 선택/필터링이 완료된 후 최종 레이아웃을 정착시키고 표시
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Canvas.ForceUpdateCanvases();
            if (inventoryContainer is RectTransform finalRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(finalRt);

            if (invGroup) invGroup.alpha = 1f;
        }

        // 인벤토리에 스폰된 블록들의 탭 카테고리를 등장 순서대로(중복 없이) 수집
        private List<BlockCategory> CollectInventoryCategories()
        {
            var categories = new List<BlockCategory>();
            foreach (Transform child in inventoryContainer)
            {
                if (!child.TryGetComponent<CodingBlock>(out CodingBlock block)) continue;

                BlockCategory tab = BlockFactory.GetTabCategory(block.Category);
                if (!categories.Contains(tab)) categories.Add(tab);
            }
            return categories;
        }

        // 시작하기/완성하기는 인벤토리 대신 코딩 패널에 초기 배치 (시작: 좌상단, 완성: 좌하단)
        private void PlaceControlBlock(GameObject go, bool isStart)
        {
            go.transform.SetParent(codingContainer, false);

            CodingBlock.ApplyControlBlockLayout(go.transform as RectTransform, isStart, codingContainer);

            if (go.TryGetComponent<CodingBlock>(out CodingBlock block))
            {
                block.SetHome(codingContainer);
                BlockFactory.AttachSockets(block);
            }
        }

        private static void Clear(Transform t)
        {
            if (!t) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
