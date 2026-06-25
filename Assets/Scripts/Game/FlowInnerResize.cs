using UnityEngine;
using UnityEngine.UI;

namespace DG.Game
{
    [RequireComponent(typeof(LayoutElement))]
    public class FlowInnerResize : MonoBehaviour
    {
        private const float MinHeight = 50f;

        private LayoutElement _le;
        private RectTransform _rt;
        private InnerSocket   _socket;
        private RectTransform _parentRt;
        private float         _socketOffset;
        private float         _bottomSocketOffset;

        private void Start()
        {
            TryGetComponent<LayoutElement>(out _le);
            TryGetComponent<RectTransform>(out _rt);
            _socket = GetComponentInChildren<InnerSocket>();
            if (transform.parent)
                transform.parent.TryGetComponent<RectTransform>(out _parentRt);

            if (_socket && _socket.TryGetComponent<RectTransform>(out var srt))
                _socketOffset = -srt.anchoredPosition.y;

            var bottomGo = transform.Find("InnerBottomSocket");
            if (bottomGo && bottomGo.TryGetComponent<RectTransform>(out var brt))
                _bottomSocketOffset = brt.anchoredPosition.y;
        }

        private void LateUpdate()
        {
            if (!_le || !_socket) return;

            float innerTarget = HasMultipleBlocks()
                ? Mathf.Max(MinHeight, ChainHeight() + _socketOffset + _bottomSocketOffset)
                : MinHeight;

            if (!Mathf.Approximately(_le.preferredHeight, innerTarget))
            {
                _le.preferredHeight = innerTarget;
                _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, innerTarget);
                if (_parentRt) LayoutRebuilder.MarkLayoutForRebuild(_parentRt);
            }

            if (!_parentRt) return;
            float totalHeight = SumChildPreferredHeights();
            if (!Mathf.Approximately(_parentRt.sizeDelta.y, totalHeight))
                _parentRt.sizeDelta = new Vector2(_parentRt.sizeDelta.x, totalHeight);
        }

        private float SumChildPreferredHeights()
        {
            float total = 0f;
            for (int i = 0; i < _parentRt.childCount; i++)
            {
                var child = _parentRt.GetChild(i);
                if (!child.TryGetComponent<LayoutElement>(out var le) || le.ignoreLayout) continue;
                total += le.preferredHeight;
            }
            return total;
        }

        private bool HasMultipleBlocks()
        {
            if (!_socket.Occupant) return false;
            ChainOutSocket outSocket = null;
            _socket.Occupant.transform.Find("ChainOutSocket")?.TryGetComponent(out outSocket);
            return outSocket && outSocket.Occupant;
        }

        private float ChainHeight()
        {
            var block = _socket.Occupant;
            ChainOutSocket lastOut = null;
            while (block)
            {
                ChainOutSocket outSocket = null;
                block.transform.Find("ChainOutSocket")?.TryGetComponent(out outSocket);
                if (outSocket) lastOut = outSocket;
                block = outSocket ? outSocket.Occupant : null;
            }

            if (!lastOut) return 0f;
            return _socket.transform.position.y - lastOut.transform.position.y;
        }
    }
}
