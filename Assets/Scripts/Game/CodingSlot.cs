using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DG.Game
{
    // 코딩 영역의 세로 슬롯. Control·Value·Logic은 받지 않음.
    public class CodingSlot : MonoBehaviour, IDropHandler
    {
        private CodingBlock _occupant;
        private Image _bg;

        private static readonly Color EmptyColor    = new(1f, 1f, 1f, 0.08f);
        private static readonly Color OccupiedColor = new(0f, 0f, 0f, 0f);

        private void Awake()
        {
            _bg = GetComponent<Image>();
            if (_bg == null) _bg = gameObject.AddComponent<Image>();
            _bg.color = EmptyColor;
        }

        public bool IsEmpty => _occupant == null;

        public void OnDrop(PointerEventData e)
        {
            var block = e.pointerDrag?.GetComponent<CodingBlock>();
            if (block == null || !IsEmpty) return;

            var cat = block.Category;
            if (cat == BlockCategory.Control || cat == BlockCategory.Value) return;

            // 이전 슬롯에서 꺼내기
            if (block.transform.parent.TryGetComponent<CodingSlot>(out var prev))
                prev.Release();

            Accept(block);
        }

        private void Accept(CodingBlock block)
        {
            _occupant = block;
            block.PlaceIn(transform);
            if (_bg != null) _bg.color = OccupiedColor;
        }

        public void Release()
        {
            _occupant = null;
            if (_bg != null) _bg.color = EmptyColor;
        }
    }
}
