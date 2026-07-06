using DG.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DG.Game
{
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private Transform inventoryContainer;

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

        public void Spawn(BlockLayoutData layout)
        {
            Clear(inventoryContainer);

            if (layout?.inventoryBlocks is null) return;

            foreach (var entry in layout.inventoryBlocks)
            {
                GameObject go = BlockFactory.Create(entry, _rootCanvas, draggable: true);
                go.transform.SetParent(inventoryContainer, false);
                _resolver?.InjectGameObject(go);
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
