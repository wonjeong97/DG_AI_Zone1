using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    // 코딩 영역의 세로 슬롯. Control·Value·Logic은 받지 않음.
    public class CodingSlot : MonoBehaviour, IDropHandler
    {
        private CodingBlock _occupant;
        private Image _bg;

        private readonly static Color EmptyColor    = new(1f, 1f, 1f, 0.08f);
        private readonly static Color OccupiedColor = new(0f, 0f, 0f, 0f);

        /// <summary>
        /// 배경 이미지를 확보하고 빈 상태 색으로 초기화한다.
        /// </summary>
        private void Awake()
        {
            if (!TryGetComponent(out _bg))
                _bg = gameObject.AddComponent<Image>();
            _bg.color = EmptyColor;
        }

        public bool IsEmpty => !_occupant;

        /// <summary>
        /// 드롭된 블록이 슬롯에 들어올 수 있으면 이전 자리에서 꺼내 이 슬롯에 넣는다.
        /// </summary>
        public void OnDrop(PointerEventData e)
        {
            if (!e.pointerDrag || !e.pointerDrag.TryGetComponent<CodingBlock>(out CodingBlock block) || !IsEmpty) return;

            BlockCategory cat = block.Category;
            if (cat == BlockCategory.Control || cat == BlockCategory.Value) return;

            // 이전 슬롯/소켓에서 꺼내기
            if (block.transform.parent.TryGetComponent<CodingSlot>(out CodingSlot prev))
                prev.Release();
            if (block.transform.parent.TryGetComponent<ChainOutSocket>(out ChainOutSocket cs))
                cs.Release();

            Accept(block);
        }

        /// <summary>
        /// 블록을 슬롯에 배치하고 점유 색으로 바꾼다.
        /// </summary>
        private void Accept(CodingBlock block)
        {
            _occupant = block;
            block.PlaceIn(transform);
            if (_bg) _bg.color = OccupiedColor;
        }

        /// <summary>
        /// 슬롯을 비우고 빈 상태 색으로 되돌린다.
        /// </summary>
        public void Release()
        {
            _occupant = null;
            if (_bg) _bg.color = EmptyColor;
        }
    }
}
