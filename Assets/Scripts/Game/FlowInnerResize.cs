using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Game
{
    [RequireComponent(typeof(LayoutElement))]
    public class FlowInnerResize : MonoBehaviour
    {
        // BlockFactory.InnerMinHeight와 같은 값 — Constants에서 단일 관리
        private const float MinHeight = Constants.Blocks.FlowInnerMinHeight;

        // 프리팹에서는 인스펙터로 연결, 코드로 만든 Inner 컨테이너는 BlockFactory가 SetSockets로 넘겨준다
        [SerializeField] private InnerSocket socket;
        [SerializeField] private RectTransform bottomSocket;

        private LayoutElement _le;
        private RectTransform _rt;
        private RectTransform _parentRt;
        private float         _socketOffset;
        private float         _bottomSocketOffset;
        private ILogger<FlowInnerResize> _logger;

        /// <summary>
        /// 로거를 주입받는다 (블록 생성 직후 BlockSpawner가 블록 계층 전체에 주입).
        /// </summary>
        [Inject]
        public void Construct(ILogger<FlowInnerResize> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 코드로 만든 Inner 컨테이너의 진입 소켓과 하단 기준점을 연결한다.
        /// </summary>
        public void SetSockets(InnerSocket innerSocket, RectTransform innerBottomSocket)
        {
            socket = innerSocket;
            bottomSocket = innerBottomSocket;
        }

        /// <summary>
        /// 레이아웃 참조와 두 소켓의 기준 오프셋을 캐싱한다.
        /// </summary>
        private void Start()
        {
            TryGetComponent<LayoutElement>(out _le);
            TryGetComponent<RectTransform>(out _rt);
            if (transform.parent)
                transform.parent.TryGetComponent<RectTransform>(out _parentRt);

            if (socket && socket.TryGetComponent<RectTransform>(out RectTransform srt))
                _socketOffset = -srt.anchoredPosition.y;
            else if (_logger != null)
                _logger.ZLogWarning($"[FlowInnerResize] {name}에 InnerSocket이 연결되지 않아 높이를 조절하지 않습니다.");

            if (bottomSocket)
                _bottomSocketOffset = bottomSocket.anchoredPosition.y;
        }

        /// <summary>
        /// 내부 체인 길이에 맞춰 Inner 높이와 부모(블록) 전체 높이를 갱신한다.
        /// </summary>
        private void LateUpdate()
        {
            if (!_le || !socket) return;

            // 블록이 하나라도 들어오면 마지막 블록의 ChainOutSocket이 InnerBottomSocket 위치에
            // 오도록 높이를 계산 (ChainHeight − 위쪽 소켓 오프셋 + 아래쪽 소켓 오프셋)
            float innerTarget = socket.Occupant
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

        /// <summary>
        /// 부모의 레이아웃 대상 자식들의 선호 높이를 합산한다.
        /// </summary>
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

        /// <summary>
        /// 진입 소켓부터 내부 체인 마지막 블록의 ChainOutSocket까지의 세로 거리를 구한다.
        /// </summary>
        private float ChainHeight()
        {
            CodingBlock block = socket.Occupant;
            ChainOutSocket lastOut = null;
            while (block)
            {
                ChainOutSocket outSocket = ChainOutSocket.OfBlock(block);
                if (outSocket) lastOut = outSocket;
                block = outSocket ? outSocket.Occupant : null;
            }

            if (!lastOut) return 0f;
            return socket.transform.position.y - lastOut.transform.position.y;
        }
    }
}
