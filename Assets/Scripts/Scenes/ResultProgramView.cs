using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game;
using Game.Runtime;
using HuliacDev.UI;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Scenes
{
    // 결과 패널 위쪽 칸(스크롤뷰)에 블록 조립 모양을 게임과 같은 블록으로 다시 조립하고, 하나씩 붙이듯 보여 준다.
    // 블록은 정해진 배율로 두고(칸보다 넓을 때만 폭에 맞춰 줄임) 칸보다 길어지면 새로 보인 블록이 보이게 아래로 자동 스크롤한다(손가락으로 끌어 볼 수도 있다).
    [RequireComponent(typeof(ScrollRect))]
    public class ResultProgramView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [Tooltip("블록 생성에 넘기는 최상위 캔버스 — 결과 블록은 끌 수 없지만 BlockFactory가 요구한다")]
        [SerializeField] private Canvas rootCanvas;
        [Tooltip("블록 배율(칸보다 넓은 묶음은 폭에 맞춰 더 줄임) — 4_Result.json의 programBlockScale이 있으면 그 값을 쓴다")]
        [SerializeField] private float blockScale = 0.6f;
        [Tooltip("블록 묶음과 칸 가장자리 사이 여백(블록 배율 적용 전 단위)")]
        [SerializeField] private float padding = 24f;
        [Tooltip("시작하기 체인과 오른쪽 함수 정의 블록 사이 가로 간격(블록 배율 적용 전 단위)")]
        [SerializeField] private float functionGap = 48f;
        [SerializeField] private float revealInterval = 0.3f;
        [SerializeField] private float revealFadeDuration = 0.15f;
        [SerializeField] private float scrollDuration = 0.3f;

        // 4_Result.json 값 — ResultSequence가 빌드 전에 넣는다
        public float BlockScale { set => blockScale = value; }
        public float RevealInterval { set => revealInterval = value; }
        public float ScrollDuration { set => scrollDuration = value; }

        // 소켓에 붙는 스냅 트윈과 안쪽 칸 높이 계산(FlowInnerResize)이 자리 잡도록 기다리는 프레임 수
        private const int LayoutSettleFrames = 3;

        private ScrollRect _scrollRect;
        private RectTransform _content;
        private IObjectResolver _resolver;
        private ILogger<ResultProgramView> _logger;
        private SoundManager _soundManager;

        // 만든 순서(블록 → 값 → 조건 → 안쪽 → 아래) — 이 순서로 하나씩 보여 준다
        private readonly List<CodingBlock> _revealOrder = new();
        private bool _isDragging;
        private Tween _scrollTween;

        /// <summary>
        /// 블록 생성용 리졸버, 로거, 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<ResultProgramView> logger, SoundManager soundManager)
        {
            _resolver = resolver;
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 스크롤뷰의 content를 캐싱한다.
        /// </summary>
        private void Awake()
        {
            TryGetComponent(out _scrollRect);
            _content = _scrollRect ? _scrollRect.content : null;
        }

        /// <summary>
        /// 자동 스크롤 트윈을 멈춘다.
        /// </summary>
        private void OnDestroy()
        {
            if (_scrollTween != null && _scrollTween.IsActive()) _scrollTween.Kill();
        }

        /// <summary>
        /// 사용자가 칸을 끌기 시작하면 자동 스크롤을 멈추고, 끄는 동안에는 하지 않는다.
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            if (_scrollTween != null && _scrollTween.IsActive()) _scrollTween.Kill();
        }

        /// <summary>
        /// 손을 떼면 다음 블록부터 다시 자동 스크롤한다.
        /// </summary>
        public void OnEndDrag(PointerEventData eventData) => _isDragging = false;

        /// <summary>
        /// 블록 조립 모양을 모두 투명한 채로 만들어 붙이고, 자리가 잡히면 칸 위쪽에 맞춰 놓는다(시작하기 체인 오른쪽에 함수 정의 블록).
        /// </summary>
        public async UniTask BuildAsync(ProgramShape shape, CancellationToken ct)
        {
            if (!_content || _resolver == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultProgramView] {name}에 스크롤뷰 content가 없거나 주입이 되지 않아 블록을 만들지 못했습니다.");
                return;
            }
            if (!rootCanvas && _logger != null) _logger.ZLogWarning($"[ResultProgramView] {name}에 rootCanvas가 할당되지 않았습니다.");

            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            _revealOrder.Clear();

            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.localScale = new Vector3(blockScale, blockScale, 1f);

            BlockFactory.BuildContext ctx = new BlockFactory.BuildContext(rootCanvas, _resolver, _logger);
            List<CodingBlock> roots = new List<CodingBlock>();
            if (shape?.Start is not null) AddRoot(roots, await CreateTreeAsync(shape.Start, ctx, ct));
            if (shape is not null)
                foreach (ProgramBlock definition in shape.FunctionDefs)
                    AddRoot(roots, await CreateTreeAsync(definition, ctx, ct));

            // 스냅 트윈을 바로 끝내고, 안쪽 칸 높이가 자식 블록에 맞춰질 때까지 레이아웃을 다시 계산한다
            foreach (CodingBlock block in _revealOrder)
                if (block) block.transform.DOComplete();
            for (int i = 0; i < LayoutSettleFrames; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            }

            PlaceRoots(roots);
        }

        /// <summary>
        /// 만든 블록을 만든 순서대로 blockInterval초 간격으로 페이드인하며 장착 효과음을 내고, 칸 밖이면 보이게 스크롤한다.
        /// </summary>
        public async UniTask PlayAsync(CancellationToken ct)
        {
            foreach (CodingBlock block in _revealOrder)
            {
                if (!block || !block.TryGetComponent(out CanvasGroup group)) continue;

                if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.BlockAssembled);
                ScrollToShow((RectTransform)block.transform);
                await group.DOFade(1f, revealFadeDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, revealInterval)), cancellationToken: ct);
            }
        }

        /// <summary>
        /// 체인·함수 정의 묶음의 맨 위 블록을 목록에 더하고 content 왼쪽 위에 고정한다(만들지 못했으면 건너뛴다).
        /// </summary>
        private static void AddRoot(List<CodingBlock> roots, CodingBlock root)
        {
            if (!root) return;

            // 프리팹 앵커(가운데)로 두면 PlaceRoots가 content 크기를 정할 때 블록이 크기의 절반만큼 밀린다 —
            // content 크기가 아직 0이라 앵커를 바꿔도 지금 자리는 그대로다
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            roots.Add(root);
        }

        /// <summary>
        /// 블록 하나를 투명하게 만들고 값·조건·안쪽·아래 소켓에 붙을 블록을 차례로 만들어 붙인다 — 맨 위 블록을 돌려준다.
        /// </summary>
        private async UniTask<CodingBlock> CreateTreeAsync(ProgramBlock node, BlockFactory.BuildContext ctx, CancellationToken ct)
        {
            GameObject go = await BlockFactory.Create(node.ToEntry(), ctx);
            // 어드레서블 로드를 기다리는 동안 씬을 떠났으면 방금 만든 블록을 지운다 — 다음 씬 루트에 남지 않게
            if (ct.IsCancellationRequested)
            {
                if (go) Destroy(go);
                ct.ThrowIfCancellationRequested();
            }
            if (!go) return null;

            go.transform.SetParent(_content, false);
            if (!go.TryGetComponent(out CodingBlock block))
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultProgramView] '{node.Label}' 블록에 CodingBlock이 없어 결과에 넣지 않습니다.");
                Destroy(go);
                return null;
            }

            if (!go.TryGetComponent(out CanvasGroup group)) group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            _revealOrder.Add(block);

            await AttachAsync(block.GetSocket<ValueOutSocket>(), node.Value, node, ctx, ct);
            await AttachAsync(block.GetSocket<ConditionOutSocket>(), node.Condition, node, ctx, ct);

            List<InnerSocket> inners = new List<InnerSocket>();
            block.GetSockets(inners);
            for (int i = 0; i < node.Inners.Count; i++)
                await AttachAsync(i < inners.Count ? inners[i] : null, node.Inners[i], node, ctx, ct);

            await AttachAsync(block.GetSocket<ChainOutSocket>(), node.Next, node, ctx, ct);
            return block;
        }

        /// <summary>
        /// 자식 블록 묶음을 만들어 소켓에 붙인다 — 붙일 소켓이 없으면 경고하고 지운다.
        /// </summary>
        private async UniTask AttachAsync(BlockSocket socket, ProgramBlock child, ProgramBlock owner, BlockFactory.BuildContext ctx, CancellationToken ct)
        {
            if (child is null) return;

            CodingBlock childBlock = await CreateTreeAsync(child, ctx, ct);
            if (!childBlock) return;

            if (socket)
            {
                socket.Accept(childBlock);
                return;
            }

            if (_logger != null) _logger.ZLogWarning($"[ResultProgramView] '{owner.Label}' 블록에 '{child.Label}'을(를) 붙일 소켓이 없어 결과에서 뺍니다.");
            RemoveFromReveal(childBlock);
            Destroy(childBlock.gameObject);
        }

        /// <summary>
        /// 지울 블록 묶음을 보여 줄 목록에서 뺀다.
        /// </summary>
        private void RemoveFromReveal(CodingBlock root)
        {
            _revealOrder.RemoveAll(b => !b || b == root || b.transform.IsChildOf(root.transform));
        }

        /// <summary>
        /// 묶음들을 칸 위쪽에 맞춰 왼쪽부터 나란히 놓고(시작하기 체인 오른쪽에 함수 정의), content 크기를 블록 전체에 맞춰 스크롤 범위를 정한다.
        /// </summary>
        private void PlaceRoots(List<CodingBlock> roots)
        {
            float left = padding;
            float bottom = 0f;
            foreach (CodingBlock root in roots)
            {
                RectTransform rootRt = (RectTransform)root.transform;
                Rect bounds = GetTreeBounds(root);
                rootRt.anchoredPosition += new Vector2(left - bounds.xMin, -padding - bounds.yMax);

                left += bounds.width + functionGap;
                bottom = Mathf.Max(bottom, padding + bounds.height);
            }

            float width = roots.Count > 0 ? left - functionGap + padding : 0f;
            float height = roots.Count > 0 ? bottom + padding : 0f;
            _content.sizeDelta = new Vector2(width, height);

            // 가로 스크롤은 하지 않으므로 칸보다 넓은 묶음(레벨4 조건 줄 등)만 폭에 맞춰 줄인다 — 세로는 줄이지 않고 스크롤한다
            float viewWidth = _scrollRect.viewport ? _scrollRect.viewport.rect.width : 0f;
            float scale = width > 0f && viewWidth > 0f ? Mathf.Min(blockScale, viewWidth / width) : blockScale;
            _content.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 묶음에 든 블록 전체를 감싸는 사각형을 content 좌표로 구한다.
        /// </summary>
        private Rect GetTreeBounds(CodingBlock root)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            Vector3[] corners = new Vector3[4];

            foreach (CodingBlock block in root.GetComponentsInChildren<CodingBlock>(true))
            {
                ((RectTransform)block.transform).GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 local = _content.InverseTransformPoint(corner);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// 블록 자신(아래에 이어진 블록 제외)이 칸 위·아래로 벗어나 있으면 보이게 스크롤한다 — 아래로 벗어나면 content를 올리고(블록이 칸보다 크면 윗부분에 맞춤),
        /// 체인 오른쪽 함수 정의처럼 위로 벗어나면 다시 내린다.
        /// </summary>
        private void ScrollToShow(RectTransform target)
        {
            if (_isDragging || !_scrollRect || !_scrollRect.viewport) return;

            RectTransform viewport = _scrollRect.viewport;
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float bottom = viewport.InverseTransformPoint(corners[0]).y;
            float top = viewport.InverseTransformPoint(corners[1]).y;

            Rect view = viewport.rect;
            float scale = _content.localScale.y;
            float margin = padding * scale;
            float aboveTop = view.yMax - margin - top;
            float belowBottom = view.yMin + margin - bottom;
            float shift = aboveTop < 0f ? aboveTop
                : belowBottom > 0f ? Mathf.Min(belowBottom, aboveTop)
                : 0f;
            if (Mathf.Approximately(shift, 0f)) return;

            float maxY = Mathf.Max(0f, _content.rect.height * scale - view.height);
            float targetY = Mathf.Clamp(_content.anchoredPosition.y + shift, 0f, maxY);

            _scrollRect.StopMovement();
            if (_scrollTween != null && _scrollTween.IsActive()) _scrollTween.Kill();
            _scrollTween = _content.DOAnchorPosY(targetY, scrollDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }
    }
}
