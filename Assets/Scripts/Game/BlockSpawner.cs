using DG.Data;
using UnityEngine;

namespace DG.Game
{
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private Transform inventoryContainer;
        [SerializeField] private Vector2 commandValueOffset = new Vector2(-16f, 0f);

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

            BlockFactory.CommandValueOffset = commandValueOffset;

            foreach (var entry in layout.inventoryBlocks)
            {
                var go = BlockFactory.Create(entry, _rootCanvas, draggable: true, withValueSlot: false);
                go.transform.SetParent(inventoryContainer, false);
            }
        }

        private static void Clear(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
