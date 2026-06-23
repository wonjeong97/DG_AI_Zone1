using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

        // ── 하이라이트 ─────────────────────────────────────────────
        private CodingBlock _snapTarget;
        private Image _chainHighlightImg;
        private Image _valueHighlightImg;
        private Image _errorHighlightImg;

        private Image GetOrFindHighlight(ref Image cache, string childName)
        {
            if (cache != null) return cache;
            foreach (var img in GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == childName) { cache = img; return img; }
            return null;
        }

        public void ShowChainHighlight()  => SetHL(GetOrFindHighlight(ref _chainHighlightImg, "ChainHighlight"), new Color(0.1f, 0.9f, 0.3f, 1f));
        public void ShowValueHighlight()  => SetHL(GetOrFindHighlight(ref _valueHighlightImg, "ValueHighlight"), new Color(0.1f, 0.9f, 0.3f, 1f));
        public void ClearSnapHighlight()  { SetHL(GetOrFindHighlight(ref _chainHighlightImg, "ChainHighlight"), Color.clear); SetHL(GetOrFindHighlight(ref _valueHighlightImg, "ValueHighlight"), Color.clear); }
        public void ShowErrorHighlight()  => SetHL(GetOrFindHighlight(ref _errorHighlightImg, "SpriteOutline"),  new Color(1f, 0.15f, 0.1f, 1f));
        public void ClearErrorHighlight() => SetHL(GetOrFindHighlight(ref _errorHighlightImg, "SpriteOutline"),  Color.clear);

        private static void SetHL(Image img, Color c) { if (img) img.color = c; }

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

            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;

            _homeParent = transform.parent;
            _homeIndex = transform.GetSiblingIndex();
            _homeAnchoredPos = _rt.anchoredPosition;

            if (_homeParent.TryGetComponent<ValueOutSocket>(out var vos))
                vos.Release();
            else if (_homeParent.TryGetComponent<ChainOutSocket>(out var cs))
            {
                cs.Release();
                // 스플라이스-아웃: 내 아래 블록을 위 소켓으로 당겨 올려 빈 자리를 채움
                var myOut   = transform.Find("ChainOutSocket")?.GetComponent<ChainOutSocket>();
                var myChild = myOut?.Occupant;
                if (myChild != null)
                {
                    myOut.Release();
                    cs.Accept(myChild);
                }
            }

            transform.SetParent(RootCanvas.transform, true);
            transform.SetAsLastSibling();
            _cg.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (RootCanvas == null) return;

            _rt.anchoredPosition += e.delta / RootCanvas.scaleFactor;
            UpdateSnapHighlight();
        }

        private void UpdateSnapHighlight()
        {
            CodingBlock newTarget = null;
            bool isValue = Category == BlockCategory.Value;

            if (isValue)
            {
                var socket = FindSnapValueOutSocket();
                if (socket != null)
                    newTarget = socket.GetComponentInParent<CodingBlock>();
            }
            else
            {
                var socket = FindSnapOutSocket();
                if (socket != null)
                    newTarget = socket.GetComponentInParent<CodingBlock>();
            }

            if (newTarget == _snapTarget) return;

            _snapTarget?.ClearSnapHighlight();
            _snapTarget = newTarget;

            if (_snapTarget == null) return;
            if (isValue) _snapTarget.ShowValueHighlight();
            else         _snapTarget.ShowChainHighlight();
        }

        public void OnEndDrag(PointerEventData e)
        {
            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;
            _cg.blocksRaycasts = true;
            IsDragHandled = false;

            if (Category == BlockCategory.Value)
            {
                var slot = FindSnapValueOutSocket();
                if (slot != null)
                {
                    IsDragHandled = true;
                    BlockFactory.AttachSockets(this);
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
                    BlockFactory.AttachSockets(this);
                    socket.Accept(this);
                    return;
                }
            }

            if (!RootCanvas || transform.parent == RootCanvas.transform)
                ReturnHomeOrRelease(e);
        }

        // 이 블록의 ChainInSocket이 후보 ChainOutSocket 반경 안에 있으면 스냅
        // ChainInSocket이 없는 인벤토리 블록은 블록 상단 중앙을 기준점으로 사용
        private ChainOutSocket FindSnapOutSocket()
        {
            Vector2 myPos;
            var myInSocket = transform.Find("ChainInSocket")?.GetComponent<ChainInSocket>();
            if (myInSocket != null)
            {
                myPos = (Vector2)myInSocket.transform.position;
            }
            else if (_rt != null)
            {
                var corners = new Vector3[4];
                _rt.GetWorldCorners(corners);
                myPos = ((Vector2)corners[1] + (Vector2)corners[2]) * 0.5f;
            }
            else return null;
            ChainOutSocket best = null;
            float minSqr = _chainSnapRadius * _chainSnapRadius;

            foreach (var candidate in FindObjectsOfType<ChainOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.CanAccept(this)) continue;
                if (candidate.GetComponentInParent<CodingZone>() == null) continue;

                var delta = myPos - (Vector2)candidate.transform.position;
                if (delta.y >= 0f) continue;

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr) { minSqr = sqr; best = candidate; }
            }

            return best;
        }

        // ValueInSocket이 후보 ValueOutSocket 반경 안에 있으면 스냅
        // ValueInSocket이 없는 인벤토리 블록은 블록 좌측 중앙을 기준점으로 사용
        private ValueOutSocket FindSnapValueOutSocket()
        {
            Vector2 myPos;
            var myInSocket = GetComponentInChildren<ValueInSocket>();
            if (myInSocket != null)
            {
                myPos = (Vector2)myInSocket.transform.position;
            }
            else if (_rt != null)
            {
                var corners = new Vector3[4];
                _rt.GetWorldCorners(corners);
                myPos = ((Vector2)corners[0] + (Vector2)corners[1]) * 0.5f; // 좌측 중앙
            }
            else return null;
            ValueOutSocket best = null;
            float minSqr = _snapRadius * _snapRadius;

            foreach (var candidate in FindObjectsOfType<ValueOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;
                if (candidate.GetComponentInParent<CodingZone>() == null) continue;

                var delta = myPos - (Vector2)candidate.transform.position;
                if (delta.x <= 0f) continue;

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr) { minSqr = sqr; best = candidate; }
            }
            return best;
        }

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
                    if (!this || transform.parent != socket) return;

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

            if (!this || transform.parent != socket) return;

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
        private void ReturnHomeOrRelease(PointerEventData e)
        {
            bool wasInSlot = _homeParent != null &&
                             (_homeParent.TryGetComponent<ValueOutSocket>(out _) ||
                              _homeParent.TryGetComponent<ChainOutSocket>(out _));

            CodingZone homeZone = null;
            bool wasInZone = !wasInSlot && _homeParent != null &&
                             _homeParent.TryGetComponent(out homeZone);

            if (wasInZone)
            {
                IsDragHandled = true;
                transform.SetParent(homeZone.transform, false);
                transform.SetSiblingIndex(_homeIndex);
                _rt.anchoredPosition = _homeAnchoredPos;
                SetHome(homeZone.transform);
                return;
            }

            if (wasInSlot)
            {
                // 커서가 코딩존 위라면 OnDrop이 이후에 발화해 존에 배치 → 소켓 복귀 불필요
                // e.hovered는 이전 경유 오브젝트를 포함하므로 릴리즈 시점 레이캐스트 결과를 사용
                var hitGo = e.pointerCurrentRaycast.gameObject;
                bool overZone = hitGo != null && hitGo.GetComponentInParent<CodingZone>() != null;
                if (!overZone)
                {
                    if (_homeParent.TryGetComponent<ChainOutSocket>(out var cos))
                        cos.Accept(this);
                    else if (_homeParent.TryGetComponent<ValueOutSocket>(out var vos))
                        vos.Accept(this);
                }
                return;
            }

            ReturnHome();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var rt = _rt != null ? _rt : GetComponent<RectTransform>();
            if (rt == null) return;

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
