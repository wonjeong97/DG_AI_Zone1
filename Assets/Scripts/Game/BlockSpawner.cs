using Cysharp.Threading.Tasks;
using DG.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DG.Game
{
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private Transform inventoryContainer;
        [SerializeField] private Transform codingContainer;

        [Inject] private IObjectResolver _resolver;

        private Canvas _rootCanvas;

        private void Awake()
        {
            _rootCanvas = inventoryContainer.GetComponentInParent<Canvas>()?.rootCanvas
                          ?? GetComponentInParent<Canvas>()?.rootCanvas
                          ?? FindSceneCanvas();
        }

        // FindObjectOfType는 DontDestroyOnLoad의 다른 전역 캔버스(FadeManager의 FadeCanvas 등)까지
        // 뒤지므로, 같은 씬에 속한 캔버스만 대상으로 폴백 탐색
        private Canvas FindSceneCanvas()
        {
            foreach (Canvas c in FindObjectsOfType<Canvas>())
                if (c.gameObject.scene == gameObject.scene)
                    return c.rootCanvas;
            return null;
        }

        public async UniTask Spawn(BlockLayoutData layout)
        {
            Clear(inventoryContainer);

            if (layout?.inventoryBlocks is null) return;

            foreach (var entry in layout.inventoryBlocks)
            {
                GameObject go = await BlockFactory.Create(entry, _rootCanvas, draggable: true);
                if (entry.category == BlockCategory.Control)
                    PlaceControlBlock(go, entry.controlRole == ControlRole.Start);
                else
                    go.transform.SetParent(inventoryContainer, false);
                _resolver?.InjectGameObject(go);
            }

            // 모든 블록 생성 및 부모 지정 완료 후, UI 레이아웃과 텍스트 크기가 완전히 계산되도록 프레임 끝까지 대기 및 강제 리빌드
            // (VerticalLayoutGroup + ContentSizeFitter 체인이 한 프레임 만에 안정화되지 않아 2회 반복)
            for (int i = 0; i < 2; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Canvas.ForceUpdateCanvases();
                if (inventoryContainer is RectTransform rt)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        // 시작하기/완성하기는 인벤토리 대신 코딩 패널에 초기 배치 (시작: 좌상단, 완성: 좌하단)
        private void PlaceControlBlock(GameObject go, bool isStart)
        {
            go.transform.SetParent(codingContainer, false);

            if (go.transform is RectTransform rt)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f, isStart ? 1f : 0f);
                rt.anchoredPosition = new Vector2(40f, isStart ? -60f : 60f);
            }

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
