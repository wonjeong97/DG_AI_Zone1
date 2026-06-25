using DG.Data;
using UnityEngine;

namespace DG.Game
{
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private Transform inventoryContainer;

        private Canvas _rootCanvas;

        private void Awake()
        {
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas
                          ?? FindObjectOfType<Canvas>().rootCanvas;
        }

        public void Spawn(BlockLayoutData layout)
        {
            Clear(inventoryContainer);

            if (layout?.inventoryBlocks == null) return;

            foreach (var entry in layout.inventoryBlocks)
            {
                var go = BlockFactory.Create(entry, _rootCanvas, draggable: true);
                go.transform.SetParent(inventoryContainer, false);
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
