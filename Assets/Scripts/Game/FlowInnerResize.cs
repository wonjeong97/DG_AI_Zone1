using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    [RequireComponent(typeof(LayoutElement))]
    public class FlowInnerResize : MonoBehaviour
    {
        // BlockFactory.InnerMinHeight와 같은 값 — Constants에서 단일 관리
        private const float MinHeight = Constants.Blocks.FlowInnerMinHeight;

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

            if (_socket && _socket.TryGetComponent<RectTransform>(out RectTransform srt))
                _socketOffset = -srt.anchoredPosition.y;

            Transform bottomGo = transform.Find(Constants.Sockets.InnerBottomName);
            if (bottomGo && bottomGo.TryGetComponent<RectTransform>(out RectTransform brt))
                _bottomSocketOffset = brt.anchoredPosition.y;
        }

        private void LateUpdate()
        {
            if (!_le || !_socket) return;

            // 블록이 하나라도 들어오면 마지막 블록의 ChainOutSocket이 InnerBottomSocket 위치에
            // 오도록 높이를 계산 (ChainHeight − 위쪽 소켓 오프셋 + 아래쪽 소켓 오프셋)
            float innerTarget = _socket.Occupant
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
                Transform child = _parentRt.GetChild(i);
                if (!child.TryGetComponent<LayoutElement>(out LayoutElement le) || le.ignoreLayout) continue;
                total += le.preferredHeight;
            }
            return total;
        }

        private float ChainHeight()
        {
            CodingBlock block = _socket.Occupant;
            ChainOutSocket lastOut = null;
            while (block)
            {
                ChainOutSocket outSocket = null;
                block.transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out outSocket);
                if (outSocket) lastOut = outSocket;
                block = outSocket ? outSocket.Occupant : null;
            }

            if (!lastOut) return 0f;
            return _socket.transform.position.y - lastOut.transform.position.y;
        }
    }
}
