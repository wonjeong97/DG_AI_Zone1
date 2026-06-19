using UnityEngine;

namespace DG.Game
{
    public class ValueSlot : MonoBehaviour
    {
        private CodingBlock _value;

        public bool IsEmpty => _value == null;

        /// <summary>
        /// Accepts a value block and triggers snap animation.
        /// Called directly by magnetic snap in CodingBlock.OnEndDrag.
        /// </summary>
        public void Accept(CodingBlock block)
        {
            _value = block;
            block.SnapInto(transform).Forget();
        }

        public void Release()
        {
            _value = null;
        }
    }
}
