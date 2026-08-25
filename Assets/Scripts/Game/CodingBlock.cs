using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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

namespace Game
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CodingBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float _snapRadius = 120f;
        [SerializeField] private float _chainSnapRadius = 120f;
        [SerializeField] private float _snapSeconds = 0.15f;

        // 레벨 5(함수) 한정 — 메인 체인(시작~완성)에는 함수 블록만 연결하도록 제한.
        // GameSceneManager가 레벨 로드 시 레이아웃에 함수 블록이 있으면 true로 설정.
        public static bool RestrictMainChainToFunction { get; set; }

        public BlockCategory Category { get; private set; }
        public ValueKind ValueKind { get; private set; }
        public Data.ControlRole ControlRole { get; private set; }
        public bool IsDragHandled { get; private set; }

        private ILogger<CodingBlock> _log;

        [Inject]
        public void Construct(ILogger<CodingBlock> log)
        {
            _log = log;
        }

        private Canvas _canvas;
        private RectTransform _rt;
        private CanvasGroup _cg;
        private Transform _homeParent;
        private int _homeIndex;
        private Vector2 _homeAnchoredPos;

        private ChainOutSocket[] _cachedChainOutSockets;
        private InnerSocket[] _cachedInnerSockets;
        private ValueOutSocket[] _cachedValueOutSockets;
        private ConditionOutSocket[] _cachedConditionOutSockets;

        private void ClearDragCache()
        {
            _cachedChainOutSockets = null;
            _cachedInnerSockets = null;
            _cachedValueOutSockets = null;
            _cachedConditionOutSockets = null;
        }

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
        public void ShowSuccessHighlight() => SetHL(GetOrFindHighlight(ref _errorHighlightImg, "SpriteOutline"), new Color(0.1f, 0.9f, 0.3f, 1f));
        public void ClearErrorHighlight() => SetHL(GetOrFindHighlight(ref _errorHighlightImg, "SpriteOutline"), Color.clear);

        private static void SetHL(Image img, Color c)
        {
            if (img) img.color = c;
        }

        public void Init(BlockCategory category, Canvas rootCanvas, ValueKind valueKind = ValueKind.None,
            Data.ControlRole controlRole = Data.ControlRole.None)
        {
            Category = category;
            ValueKind = valueKind;
            ControlRole = controlRole;
            _canvas = rootCanvas;
            TryGetComponent<RectTransform>(out _rt);
            TryGetComponent<CanvasGroup>(out _cg);
        }

        private void OnDisable()
        {
            if (!_cg) TryGetComponent(out _cg);
            if (_cg) _cg.blocksRaycasts = true;
            IsDragHandled = false;
            ClearDragCache();
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

            _cachedChainOutSockets = FindObjectsOfType<ChainOutSocket>();
            _cachedInnerSockets = FindObjectsOfType<InnerSocket>();
            _cachedValueOutSockets = FindObjectsOfType<ValueOutSocket>();
            _cachedConditionOutSockets = FindObjectsOfType<ConditionOutSocket>();

            // 코딩을 다시 건드리기 시작하면 이전 빌드 결과(성공/에러 외곽선)는 더 이상 유효하지 않으므로 정리
            foreach (CodingBlock b in FindObjectsOfType<CodingBlock>())
                b.ClearErrorHighlight();

            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;

            // 진행 중인 스냅 트윈을 즉시 완료 — 리페런트 후 잔여 틱이 캔버스 좌표계에 적용되어
            // 블록이 좌상단으로 날아가는 문제 방지 (홈 위치도 정착 좌표로 기록되도록 드래그 상태 저장 전에 수행)
            DOTween.Kill(_rt, true);

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
            transform.Find(Constants.Sockets.ChainOutName)?.TryGetComponent(out myOut);
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
                        ClearDragCache();
                        return;
                    }
                }

                ValueOutSocket slot = FindSnapValueOutSocket();
                if (slot)
                {
                    IsDragHandled = true;
                    BlockFactory.AttachSockets(this);
                    slot.Accept(this);
                    ClearDragCache();
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
                    ClearDragCache();
                    return;
                }

                if (innerSocket)
                {
                    IsDragHandled = true;
                    BlockFactory.AttachSockets(this);
                    innerSocket.Accept(this);
                    ClearDragCache();
                    return;
                }
            }

            if (!RootCanvas || transform.parent == RootCanvas.transform)
                ReturnHomeOrRelease(e);
            
            ClearDragCache();
        }

        // ChainInSocket 위치 또는 블록 상단 중앙을 스냅 기준점으로 반환
        private bool TryGetChainSnapOrigin(out Vector2 pos)
        {
            ChainInSocket inSocket = null;
            transform.Find(Constants.Sockets.ChainInName)?.TryGetComponent(out inSocket);
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

            var candidates = _cachedChainOutSockets ?? FindObjectsOfType<ChainOutSocket>();
            foreach (ChainOutSocket candidate in candidates)
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
            if (Category == BlockCategory.Control || !TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            InnerSocket best = null;
            float minSqr = _chainSnapRadius * _chainSnapRadius;

            var candidates = _cachedInnerSockets ?? FindObjectsOfType<InnerSocket>();
            foreach (InnerSocket candidate in candidates)
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

            var candidates = _cachedValueOutSockets ?? FindObjectsOfType<ValueOutSocket>();
            foreach (ValueOutSocket candidate in candidates)
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;
                if (!candidate.GetComponentInParent<CodingZone>()) continue;

                CodingBlock targetBlock = candidate.GetComponentInParent<CodingBlock>();
                if (targetBlock)
                {
                    if (Category == BlockCategory.Value)
                    {
                        if (targetBlock.Category != BlockCategory.Command) continue;
                        // Command가 허용하는 값 타입만 스냅 (None인 Command는 소켓이 없어 대상에서 제외됨)
                        if (targetBlock.ValueKind != ValueKind.None && targetBlock.ValueKind != ValueKind) continue;
                    }
                    // Logic(그리고)은 조건 블록의 ConditionOut에만 연결 — 만약 헤더 슬롯 직접 스냅 금지
                    if (Category == BlockCategory.Logic) continue;
                    if (Category == BlockCategory.Condition && targetBlock.Category != BlockCategory.FlowControl && targetBlock.Category != BlockCategory.Logic) continue;

                    // 반복하기의 헤더 슬롯은 조건용이 아니므로 조건 블록은 스냅 제외 (만약 전용)
                    if (Category == BlockCategory.Condition
                        && targetBlock.Category == BlockCategory.FlowControl && targetBlock.name.Contains("반복"))
                        continue;
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

            var candidates = _cachedConditionOutSockets ?? FindObjectsOfType<ConditionOutSocket>();
            foreach (ConditionOutSocket candidate in candidates)
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

            try
            {
                // OutBack 이징으로 스냅 손맛 부여, 드래그 등으로 부모가 바뀌면 트윈 중단
                Tween tween = null;
                tween = _rt.DOAnchorPos(targetOffset, _snapSeconds)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject)
                    .OnUpdate(() =>
                    {
                        if (transform.parent != socket) tween.Kill();
                    });
                await tween.ToUniTask(cancellationToken: destroyCancellationToken);
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

        private Transform _inventoryParent;

        public void SetInventoryHome(Transform invParent)
        {
            _inventoryParent = invParent;
        }

        public Transform InventoryParent
        {
            get
            {
                if (!_inventoryParent)
                {
                    var categoryZone = FindObjectOfType<CategoryZone>();
                    if (categoryZone) _inventoryParent = categoryZone.InventoryContent;
                }
                return _inventoryParent;
            }
        }

        public void SetHome(Transform parent)
        {
            _homeParent = parent;
        }

        public void ReleaseAllAttachedChildren()
        {
            foreach (InnerSocket innerSocket in GetComponentsInChildren<InnerSocket>(true))
            {
                if (innerSocket.Occupant)
                {
                    CodingBlock child = innerSocket.Occupant;
                    innerSocket.Release();
                    child.ReleaseAllAttachedChildren();
                    child.ReturnToInventory();
                }
            }

            foreach (ValueOutSocket valSocket in GetComponentsInChildren<ValueOutSocket>(true))
            {
                if (valSocket.Occupant)
                {
                    CodingBlock child = valSocket.Occupant;
                    valSocket.Release();
                    child.ReleaseAllAttachedChildren();
                    child.ReturnToInventory();
                }
            }

            foreach (ConditionOutSocket condSocket in GetComponentsInChildren<ConditionOutSocket>(true))
            {
                if (condSocket.Occupant)
                {
                    CodingBlock child = condSocket.Occupant;
                    condSocket.Release();
                    child.ReleaseAllAttachedChildren();
                    child.ReturnToInventory();
                }
            }

            foreach (ChainOutSocket chainSocket in GetComponentsInChildren<ChainOutSocket>(true))
            {
                if (chainSocket.Occupant)
                {
                    CodingBlock child = chainSocket.Occupant;
                    chainSocket.Release();
                    child.ReleaseAllAttachedChildren();
                    child.ReturnToInventory();
                }
            }
        }

        public static void ResetControlBlockPosition(CodingBlock block, Transform codingZone)
        {
            if (!block || !codingZone) return;

            block.transform.SetParent(codingZone, false);
            block.SetHome(codingZone);

            if (block.transform is RectTransform rt)
            {
                bool isStart = block.ControlRole == Data.ControlRole.Start;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, isStart ? 1f : 0f);
                float y = isStart ? -120f : 120f;

                ScrollRect scroll = codingZone.GetComponentInParent<ScrollRect>();
                if (!isStart && scroll && scroll.viewport && codingZone is RectTransform content)
                {
                    float overflow = content.rect.height - scroll.viewport.rect.height;
                    if (overflow > 0f) y += overflow;
                }

                rt.anchoredPosition = new Vector2(80f, y);
            }

            block.gameObject.SetActive(true);
        }

        public void ReturnToInventory()
        {
            if (Category == BlockCategory.Control)
            {
                CodingZone codingZone = FindObjectOfType<CodingZone>();
                if (codingZone)
                {
                    ResetControlBlockPosition(this, codingZone.transform);
                }
                return;
            }

            ReleaseAllAttachedChildren();

            Transform targetParent = InventoryParent;
            if (targetParent)
            {
                transform.SetParent(targetParent, false);
                SetHome(targetParent);

                var categoryZone = FindObjectOfType<CategoryZone>();
                if (categoryZone)
                {
                    gameObject.SetActive(BlockFactory.GetTabCategory(Category) == categoryZone.CurrentCategory);
                }
            }
            else if (_homeParent)
            {
                transform.SetParent(_homeParent, false);
            }
        }

        public void ReturnHome()
        {
            if (!_homeParent) return;

            bool isReturningToInventory = _homeParent.GetComponentInParent<CodingZone>() == null;
            if (isReturningToInventory)
            {
                ReturnToInventory();
                return;
            }

            transform.SetParent(_homeParent, false);
            transform.SetSiblingIndex(_homeIndex);
            if (!_rt) TryGetComponent(out _rt);
            if (_rt) _rt.anchoredPosition = _homeAnchoredPos;

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
                _log?.ZLogWarning($"[CodingBlock] CodingZone을 찾을 수 없습니다.");
                ReturnToInventory();
                return;
            }

            if (!zone.TryGetComponent(out RectTransform zoneRect))
                zoneRect = zone.GetComponentInParent<RectTransform>();

            if (!zoneRect)
            {
                _log?.ZLogWarning($"[CodingBlock] CodingZone 영역에 RectTransform이 없습니다.");
                ReturnToInventory();
                return;
            }

            // 스크롤 존이면 확대된 Content가 아니라 화면에 보이는 Viewport 기준으로 내부 판정
            ScrollRect scroll = zone.GetComponentInParent<ScrollRect>();
            if (scroll && scroll.viewport) zoneRect = scroll.viewport;

            bool isInside = RectTransformUtility.RectangleContainsScreenPoint(zoneRect, e.position, e.pressEventCamera);

            if (isInside)
            {
                IsDragHandled = true;
                transform.SetParent(zone.transform, true);
                SetHome(zone.transform);
            }
            else
            {
                ReturnToInventory();
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