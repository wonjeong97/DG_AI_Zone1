using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using ZLogger;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CodingBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float _snapRadius      = 120f;
        [SerializeField] private float _chainSnapRadius = 120f;
        [SerializeField] private float _snapSeconds     = 0.15f;

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

            if (_homeParent.TryGetComponent<ValueOutSocket>(out var vos))
                vos.Release();
            else if (_homeParent.TryGetComponent<ChainOutSocket>(out var cs))
                cs.Release();

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
                var slot = FindSnapValueOutSocket();
                if (slot != null)
                {
                    IsDragHandled = true;
                    slot.Accept(this);
                    return;
                }
            }
            else
            {
                var socket = FindSnapOutSocket();
                if (socket != null)
                {
                    IsDragHandled = true;
                    socket.Accept(this);
                    return;
                }
            }

            if (!RootCanvas || transform.parent == RootCanvas.transform)
                ReturnHomeOrRelease();
        }

        /// <summary>
        /// Finds the nearest MonoBehaviour of type T within pixel-space radius.
        /// World-position comparison stays correct across canvas scales.
        /// </summary>
        // 이 블록의 ChainInSocket이 후보 ChainOutSocket 반경 안에 있으면 스냅
        private ChainOutSocket FindSnapOutSocket()
        {
            var myInSocket = transform.Find("ChainInSocket")?.GetComponent<ChainInSocket>();
            if (myInSocket == null) return null;

            var myPos = (Vector2)myInSocket.transform.position;
            ChainOutSocket best = null;
            float minSqr = _chainSnapRadius * _chainSnapRadius;

            foreach (var candidate in FindObjectsOfType<ChainOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.CanAccept(this)) continue;

                var delta = myPos - (Vector2)candidate.transform.position;
                if (delta.y >= 0f) continue; // InSocket이 OutSocket 아래(3·4사분면)에 있어야 함

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr) { minSqr = sqr; best = candidate; }
            }

            return best;
        }

        // ValueInSocket이 후보 ValueOutSocket 반경 안에 있으면 스냅
        private ValueOutSocket FindSnapValueOutSocket()
        {
            var myInSocket = GetComponentInChildren<ValueInSocket>();
            if (myInSocket == null) return null;

            var myPos = (Vector2)myInSocket.transform.position;
            ValueOutSocket best = null;
            float minSqr = _snapRadius * _snapRadius;

            foreach (var candidate in FindObjectsOfType<ValueOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;

                var delta = myPos - (Vector2)candidate.transform.position;
                if (delta.x <= 0f) continue;  // 1·4사분면만 허용 (InSocket이 OutSocket 오른쪽)

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr) { minSqr = sqr; best = candidate; }
            }
            return best;
        }

        /// <summary>
        /// Reparents block to socket then lerps anchoredPosition to targetOffset (default zero).
        /// worldPositionStays on reparent prevents visual jump before lerp.
        /// </summary>
        public async UniTaskVoid SnapInto(Transform socket, Vector2 targetOffset = default)
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
                        startPos, targetOffset, Mathf.Clamp01(elapsed / _snapSeconds));
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

            _rt.anchoredPosition = targetOffset;
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

            if (_homeParent.TryGetComponent<ValueOutSocket>(out var vos))
                vos.Reoccupy(this);
            else if (_homeParent.TryGetComponent<ChainOutSocket>(out var cos))
                cos.Reoccupy(this);
        }

        // 슬롯/소켓 출신이거나 CodingZone 소속 블록은 현재 드롭 위치로 CodingZone에 착지
        // 인벤토리 출신 블록만 ReturnHome
        private void ReturnHomeOrRelease()
        {
            bool wasInSlot = _homeParent != null &&
                             (_homeParent.TryGetComponent<ValueOutSocket>(out _) ||
                              _homeParent.TryGetComponent<ChainOutSocket>(out _));

            CodingZone homeZone = null;
            bool wasInZone = !wasInSlot && _homeParent != null &&
                             _homeParent.TryGetComponent(out homeZone);

            if (wasInSlot || wasInZone)
            {
                var zone = wasInZone ? homeZone : FindObjectOfType<CodingZone>();

                if (zone != null)
                {
                    IsDragHandled = true;
                    transform.SetParent(zone.transform, true);
                    SetHome(zone.transform);
                    return;
                }
            }

            ReturnHome();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var rt = _rt != null ? _rt : GetComponent<RectTransform>();
            if (rt == null) return;

            // 스냅 반경
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(rt.position, _snapRadius);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(rt.position, 4f);

            Handles.Label(rt.position + Vector3.up * (_snapRadius + 10f),
                $"snap r={_snapRadius}",
                new GUIStyle { normal = { textColor = new Color(0.3f, 0.7f, 1f) }, fontSize = 9 });
        }
#endif
    }
}