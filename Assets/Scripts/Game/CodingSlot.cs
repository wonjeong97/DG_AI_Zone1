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

        private readonly static Color EmptyColor    = new(1f, 1f, 1f, 0.08f);
        private readonly static Color OccupiedColor = new(0f, 0f, 0f, 0f);

        private void Awake()
        {
            if (!TryGetComponent(out _bg))
                _bg = gameObject.AddComponent<Image>();
            _bg.color = EmptyColor;
        }

        public bool IsEmpty => !_occupant;

        public void OnDrop(PointerEventData e)
        {
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out CodingBlock block) || !IsEmpty) return;

            var cat = block.Category;
            if (cat == BlockCategory.Control || cat == BlockCategory.Value) return;

            // 이전 슬롯/소켓에서 꺼내기
            if (block.transform.parent.TryGetComponent<CodingSlot>(out CodingSlot prev))
                prev.Release();
            if (block.transform.parent.TryGetComponent<ChainOutSocket>(out ChainOutSocket cs))
                cs.Release();

            Accept(block);
        }

        private void Accept(CodingBlock block)
        {
            _occupant = block;
            block.PlaceIn(transform);
            if (_bg) _bg.color = OccupiedColor;
        }

        public void Release()
        {
            _occupant = null;
            if (_bg) _bg.color = EmptyColor;
        }
    }
}
