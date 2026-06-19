using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using ZLogger;

namespace DG.Game
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CodingBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float _snapRadius  = 120f;
        [SerializeField] private float _snapSeconds = 0.15f;

        public BlockCategory Category { get; private set; }
        public bool IsDragHandled { get; private set; }

        [Inject] private ILogger<CodingBlock> _log;

        private Canvas _canvas;
        private RectTransform _rt;
        private CanvasGroup _cg;
        private Transform _homeParent;
        private int _homeIndex;
        private Vector2 _homeAnchoredPos;

        public void Init(BlockCategory category, Canvas rootCanvas)
        {
            Category = category;
            _canvas = rootCanvas;
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
        }

        // 드래그 시점에 캔버스를 다시 확인 (Init이 배치 전 호출될 수 있으므로)
        private Canvas RootCanvas
        {
            get
            {
                if (_canvas == null)
                    _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
                return _canvas;
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (RootCanvas == null) return;

            _homeParent = transform.parent;
            _homeIndex = transform.GetSiblingIndex();
            _homeAnchoredPos = _rt.anchoredPosition;

            transform.SetParent(RootCanvas.transform, true);
            transform.SetAsLastSibling();
            _cg.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (RootCanvas == null) return;
            _rt.anchoredPosition += e.delta / RootCanvas.scaleFactor;
        }

        /// <summary>
        /// Ends drag and attempts magnetic snap to nearest compatible socket.
        /// Socket snap takes priority over free placement.
        /// </summary>
        public void OnEndDrag(PointerEventData e)
        {
            _cg.blocksRaycasts = true;
            IsDragHandled = false;

            if (Category == BlockCategory.Value)
            {
                var slot = FindNearestSocket<ValueSlot>(_snapRadius);
                if (slot && slot.IsEmpty)
                {
                    IsDragHandled = true;
                    slot.Accept(this);
                    return;
                }
            }

            if (!RootCanvas || transform.parent == RootCanvas.transform)
                ReturnHome();
        }

        /// <summary>
        /// Finds the nearest MonoBehaviour of type T within pixel-space radius.
        /// World-position comparison stays correct across canvas scales.
        /// </summary>
        private T FindNearestSocket<T>(float radius) where T : MonoBehaviour
        {
            T nearest = null;
            float minSqr = radius * radius;
            var selfPos = (Vector2)_rt.position;

            foreach (var candidate in FindObjectsOfType<T>())
            {
                if (!(candidate.transform is RectTransform rt)) continue;
                float sqr = ((Vector2)rt.position - selfPos).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Reparents block to socket then lerps anchoredPosition to zero.
        /// worldPositionStays on reparent prevents visual jump before lerp.
        /// </summary>
        public async UniTaskVoid SnapInto(Transform socket)
        {
            transform.SetParent(socket, true);
            _homeParent = socket;

            var startPos = _rt.anchoredPosition;
            float elapsed = 0f;
            try
            {
                while (elapsed < _snapSeconds)
                {
                    if (!this) return;
                    elapsed += Time.deltaTime;
                    _rt.anchoredPosition = Vector2.Lerp(
                        startPos, Vector2.zero, Mathf.Clamp01(elapsed / _snapSeconds));
                    await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _log?.ZLogError(ex, $"SnapInto failed on {name}");
                return;
            }

            if (!this) return;
            _rt.anchoredPosition = Vector2.zero;
        }

        public void PlaceIn(Transform parent)
        {
            transform.SetParent(parent, false);
            _rt.anchoredPosition = Vector2.zero;
            _homeParent = parent;
        }

        public void SetHome(Transform parent)
        {
            _homeParent = parent;
        }

        public void ReturnHome()
        {
            transform.SetParent(_homeParent, false);
            transform.SetSiblingIndex(_homeIndex);
            _rt.anchoredPosition = _homeAnchoredPos;
        }
    }
}
