using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using ZLogger;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Game
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CodingBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float _snapRadius = 120f;
        [SerializeField] private float _chainSnapRadius = 120f;
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

        // ── 하이라이트 ─────────────────────────────────────────────
        private CodingBlock _snapTarget;
        private Image _chainHighlightImg;
        private Image _valueHighlightImg;
        private Image _errorHighlightImg;

        private Image GetOrFindHighlight(ref Image cache, string childName)
        {
            if (cache) return cache;

            foreach (Image img in GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == childName)
                {
                    cache = img;
                    return img;
                }

            return null;
        }

        public void ShowChainHighlight() => SetHL(GetOrFindHighlight(ref _chainHighlightImg, "ChainHighlight"), new Color(0.1f, 0.9f, 0.3f, 1f));
        public void ShowValueHighlight() => SetHL(GetOrFindHighlight(ref _valueHighlightImg, "ValueHighlight"), new Color(0.1f, 0.9f, 0.3f, 1f));

        public void ClearSnapHighlight()
        {
            SetHL(GetOrFindHighlight(ref _chainHighlightImg, "ChainHighlight"), Color.clear);
            SetHL(GetOrFindHighlight(ref _valueHighlightImg, "ValueHighlight"), Color.clear);
        }

        public void ShowErrorHighlight() => SetHL(GetOrFindHighlight(ref _errorHighlightImg, "SpriteOutline"), new Color(1f, 0.15f, 0.1f, 1f));
        public void ClearErrorHighlight() => SetHL(GetOrFindHighlight(ref _errorHighlightImg, "SpriteOutline"), Color.clear);

        private static void SetHL(Image img, Color c)
        {
            if (img) img.color = c;
        }

        public void Init(BlockCategory category, Canvas rootCanvas)
        {
            Category = category;
            _canvas = rootCanvas;
            TryGetComponent<RectTransform>(out _rt);
            TryGetComponent<CanvasGroup>(out _cg);
        }

        // 드래그 시점에 캔버스를 다시 확인 (Init이 배치 전 호출될 수 있으므로)
        private Canvas RootCanvas
        {
            get
            {
                if (!_canvas)
                    _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
                return _canvas;
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!RootCanvas) return;

            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;

            _homeParent = transform.parent;
            _homeIndex = transform.GetSiblingIndex();
            _homeAnchoredPos = _rt.anchoredPosition;

            if (_homeParent.TryGetComponent<ValueOutSocket>(out ValueOutSocket vos))
                vos.Release();
            else if (_homeParent.TryGetComponent<ConditionOutSocket>(out ConditionOutSocket condOut))
                condOut.Release();
            else if (_homeParent.TryGetComponent<ChainOutSocket>(out ChainOutSocket cs))
            {
                cs.Release();
                SpliceOutChild(cs.Accept);
            }
            else if (_homeParent.TryGetComponent<InnerSocket>(out InnerSocket ins))
            {
                ins.Release();
                SpliceOutChild(ins.Accept);
            }

            transform.SetParent(RootCanvas.transform, true);
            transform.SetAsLastSibling();
            _cg.blocksRaycasts = false;
        }

        private void SpliceOutChild(Action<CodingBlock> acceptToParent)
        {
            ChainOutSocket myOut = null;
            transform.Find("ChainOutSocket")?.TryGetComponent(out myOut);
            CodingBlock myChild = myOut ? myOut.Occupant : null;
            if (!myChild) return;

            myOut.Release();
            acceptToParent(myChild);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!RootCanvas) return;

            _rt.anchoredPosition += e.delta / RootCanvas.scaleFactor;
            UpdateSnapHighlight();
        }

        private void UpdateSnapHighlight()
        {
            CodingBlock newTarget = null;
            bool isValue = Category == BlockCategory.Value || Category == BlockCategory.Logic || Category == BlockCategory.Condition;

            if (isValue)
            {
                // Condition / Logic: ConditionOut 스냅 우선, 없으면 ValueOut 스냅
                if (Category == BlockCategory.Condition || Category == BlockCategory.Logic)
                {
                    ConditionOutSocket condSocket = FindSnapConditionOutSocket();
                    if (condSocket)
                        newTarget = condSocket.GetComponentInParent<CodingBlock>();
                }

                if (!newTarget)
                {
                    ValueOutSocket socket = FindSnapValueOutSocket();
                    if (socket)
                        newTarget = socket.GetComponentInParent<CodingBlock>();
                }
            }
            else
            {
                ChainOutSocket chainSocket = FindSnapOutSocket(out float chainSqr);
                FindSnapInnerSocket(out float innerSqr);

                // 두 범위가 겹치면 더 가까운 쪽 우선 — InnerSocket이 가까우면 하이라이트 없음
                if (chainSocket && chainSqr <= innerSqr)
                    newTarget = chainSocket.GetComponentInParent<CodingBlock>();
            }

            if (newTarget == _snapTarget) return;

            _snapTarget?.ClearSnapHighlight();
            _snapTarget = newTarget;

            if (!_snapTarget) return;

            if (isValue) _snapTarget.ShowValueHighlight();
            else _snapTarget.ShowChainHighlight();
        }

        public void OnEndDrag(PointerEventData e)
        {
            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;
            _cg.blocksRaycasts = true;
            IsDragHandled = false;

            bool isValue = Category == BlockCategory.Value || Category == BlockCategory.Logic || Category == BlockCategory.Condition;

            if (isValue)
            {
                // Condition / Logic: ConditionOut 스냅 우선
                if (Category == BlockCategory.Condition || Category == BlockCategory.Logic)
                {
                    ConditionOutSocket condSlot = FindSnapConditionOutSocket();
                    if (condSlot)
                    {
                        IsDragHandled = true;
                        BlockFactory.AttachSockets(this);
                        condSlot.Accept(this);
                        return;
                    }
                }

                ValueOutSocket slot = FindSnapValueOutSocket();
                if (slot)
                {
                    IsDragHandled = true;
                    BlockFactory.AttachSockets(this);
                    slot.Accept(this);
                    return;
                }
            }
            else
            {
                ChainOutSocket chainSocket = FindSnapOutSocket(out float chainSqr);
                InnerSocket innerSocket = FindSnapInnerSocket(out float innerSqr);

                if (chainSocket && (!innerSocket || chainSqr <= innerSqr))
                {
                    IsDragHandled = true;
                    BlockFactory.AttachSockets(this);
                    chainSocket.Accept(this);
                    return;
                }

                if (innerSocket)
                {
                    IsDragHandled = true;
                    BlockFactory.AttachSockets(this);
                    innerSocket.Accept(this);
                    return;
                }
            }

            if (!RootCanvas || transform.parent == RootCanvas.transform)
                ReturnHomeOrRelease(e);
        }

        // ChainInSocket 위치 또는 블록 상단 중앙을 스냅 기준점으로 반환
        private bool TryGetChainSnapOrigin(out Vector2 pos)
        {
            ChainInSocket inSocket = null;
            transform.Find("ChainInSocket")?.TryGetComponent(out inSocket);
            if (inSocket)
            {
                pos = (Vector2)inSocket.transform.position;
                return true;
            }

            if (_rt)
            {
                Vector3[] corners = new Vector3[4];
                _rt.GetWorldCorners(corners);
                pos = ((Vector2)corners[1] + (Vector2)corners[2]) * 0.5f;
                return true;
            }

            pos = default;
            return false;
        }

        // 이 블록의 ChainInSocket이 후보 ChainOutSocket 반경 안에 있으면 스냅
        // ChainInSocket이 없는 인벤토리 블록은 블록 상단 중앙을 기준점으로 사용
        private ChainOutSocket FindSnapOutSocket(out float bestSqr)
        {
            if (!TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            ChainOutSocket best = null;
            float minSqr = _chainSnapRadius * _chainSnapRadius;

            foreach (ChainOutSocket candidate in FindObjectsOfType<ChainOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.CanAccept(this)) continue;
                if (!candidate.GetComponentInParent<CodingZone>()) continue;

                Vector2 delta = myPos - (Vector2)candidate.transform.position;
                if (delta.y >= 0f) continue;

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    best = candidate;
                }
            }

            bestSqr = best ? minSqr : float.MaxValue;
            return best;
        }

        // InnerSocket: 방향 제한 없이 반경 안이면 스냅 (FlowControl 내부 진입)
        private InnerSocket FindSnapInnerSocket(out float bestSqr)
        {
            if (!TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            InnerSocket best = null;
            float minSqr = _chainSnapRadius * _chainSnapRadius;

            foreach (InnerSocket candidate in FindObjectsOfType<InnerSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.CanAccept(this)) continue;
                if (!candidate.GetComponentInParent<CodingZone>()) continue;

                Vector2 delta = myPos - (Vector2)candidate.transform.position;
                if (delta.y >= 0f) continue; // 3·4사분면(하단)만 허용

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    best = candidate;
                }
            }

            bestSqr = best ? minSqr : float.MaxValue;
            return best;
        }

        /// <summary>
        /// 허용된 블록 조합 규칙에 맞는 우측 연결부 탐색.
        /// </summary>
        private ValueOutSocket FindSnapValueOutSocket()
        {
            Vector2 myPos;
            ValueInSocket myInSocket = GetComponentInChildren<ValueInSocket>();
            if (myInSocket)
                myPos = (Vector2)myInSocket.transform.position;
            else if (_rt)
            {
                Vector3[] corners = new Vector3[4];
                _rt.GetWorldCorners(corners);
                myPos = ((Vector2)corners[0] + (Vector2)corners[1]) * 0.5f;
            }
            else return null;

            ValueOutSocket best = null;
            float minSqr = _snapRadius * _snapRadius;

            foreach (ValueOutSocket candidate in Object.FindObjectsOfType<ValueOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;
                if (!candidate.GetComponentInParent<CodingZone>()) continue;

                CodingBlock targetBlock = candidate.GetComponentInParent<CodingBlock>();
                if (targetBlock)
                {
                    if (Category == BlockCategory.Value && targetBlock.Category != BlockCategory.Command) continue;
                    if (Category == BlockCategory.Logic && targetBlock.Category != BlockCategory.FlowControl) continue;
                    if (Category == BlockCategory.Condition && targetBlock.Category != BlockCategory.FlowControl && targetBlock.Category != BlockCategory.Logic) continue;
                }

                Vector2 delta = myPos - (Vector2)candidate.transform.position;
                if (delta.x <= 0f) continue;

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        // ConditionInSocket 기준으로 가장 가까운 ConditionOutSocket 탐색
        private ConditionOutSocket FindSnapConditionOutSocket()
        {
            ConditionInSocket myInSocket = GetComponentInChildren<ConditionInSocket>();
            Vector2 myPos;
            if (myInSocket)
                myPos = (Vector2)myInSocket.transform.position;
            else if (_rt)
            {
                Vector3[] corners = new Vector3[4];
                _rt.GetWorldCorners(corners);
                myPos = ((Vector2)corners[0] + (Vector2)corners[1]) * 0.5f;
            }
            else return null;

            ConditionOutSocket best = null;
            float minSqr = _snapRadius * _snapRadius;

            foreach (ConditionOutSocket candidate in Object.FindObjectsOfType<ConditionOutSocket>())
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;
                if (!candidate.GetComponentInParent<CodingZone>()) continue;

                CodingBlock targetBlock = candidate.GetComponentInParent<CodingBlock>();
                if (targetBlock)
                {
                    // Logic(그리고/또는)은 Condition 블록의 ConditionOut에 스냅
                    if (Category == BlockCategory.Logic && targetBlock.Category != BlockCategory.Condition) continue;
                    // Condition 블록은 Logic 블록의 ConditionOut에 스냅
                    if (Category == BlockCategory.Condition && targetBlock.Category != BlockCategory.Logic) continue;
                }

                Vector2 delta = myPos - (Vector2)candidate.transform.position;
                if (delta.x <= 0f) continue;

                float sqr = delta.sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        public async UniTaskVoid SnapInto(Transform socket, Vector2 targetOffset = default)
        {
            transform.SetParent(socket, true);
            _homeParent = socket;

            Vector2 startPos = _rt.anchoredPosition;
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

            if (_homeParent.TryGetComponent<ValueOutSocket>(out ValueOutSocket vos))
                vos.Reoccupy(this);
            else if (_homeParent.TryGetComponent<ConditionOutSocket>(out ConditionOutSocket condOut))
                condOut.Reoccupy(this);
            else if (_homeParent.TryGetComponent<ChainOutSocket>(out ChainOutSocket cos))
                cos.Reoccupy(this);
        }

        /// <summary>
        /// 포인터 위치가 코딩 영역 내부인지 판별하여 배치 상태를 결정합니다.
        /// </summary>
        private void ReturnHomeOrRelease(PointerEventData e)
        {
            CodingZone zone = FindObjectOfType<CodingZone>();
            if (!zone)
            {
                Debug.LogWarning("[CodingBlock] CodingZone을 찾을 수 없습니다.");
                ReturnHome();
                return;
            }

            bool hasRect = zone.TryGetComponent<RectTransform>(out RectTransform zoneRect);
            if (!hasRect)
            {
                Debug.LogWarning("[CodingBlock] CodingZone에 RectTransform이 없습니다.");
                ReturnHome();
                return;
            }

            bool isInside = RectTransformUtility.RectangleContainsScreenPoint(zoneRect, e.position, e.pressEventCamera);

            if (isInside)
            {
                IsDragHandled = true;
                transform.SetParent(zone.transform, true);
                SetHome(zone.transform);
            }
            else
            {
                ReturnHome();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_rt) TryGetComponent<RectTransform>(out _rt);
            RectTransform rt = _rt;
            if (!rt) return;

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