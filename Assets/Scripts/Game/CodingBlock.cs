using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using ZLogger;
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

        // 컴파일 성공/에러 표시 방식 — GameSceneManager가 레벨 로드 시 Inspector 설정값으로 초기화.
        public static HighlightMode Mode { get; set; } = HighlightMode.Outline;

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
        private InnerSocket _snapInnerSocket;
        private Image _chainHighlightImg;
        private Image _valueHighlightImg;
        private Image _errorHighlightImg;
        private Image _bodyImg;
        private Color _bodyOriginalColor;
        private bool _bodyColorCached;

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

        private Tweener _snapHighlightTween;
        private Image _activeSnapImage;

        // ── 스냅 하이라이트 (드래그 중 연결 가능 지점 표시 — 부드러운 깜빡임 펄스 연출) ──
        public void ShowChainHighlight() => PlaySnapPulse(ChainHighlight, isVerticalChain: true);
        public void ShowValueHighlight() => PlaySnapPulse(ValueHighlight, isVerticalChain: false);

        public void ClearSnapHighlight()
        {
            StopSnapPulse();
            SetHighlight(ChainHighlight, Color.clear);
            SetHighlight(ValueHighlight, Color.clear);
        }

        private void PlaySnapPulse(Image img, bool isVerticalChain = false)
        {
            if (!img) return;
            if (_activeSnapImage == img && _snapHighlightTween != null && _snapHighlightTween.IsActive()) return;

            StopSnapPulse();

            float t = Constants.HighlightSettings.OutlineThickness;
            RectTransform rt = img.rectTransform;
            if (rt)
            {
                if (isVerticalChain)
                {
                    // ChainHighlight: 좌우(X) 0, 상하(Y) -10~10 확장
                    rt.offsetMin = new Vector2(0f, -t);
                    rt.offsetMax = new Vector2(0f, t);
                }
                else
                {
                    rt.offsetMin = new Vector2(-t, -t);
                    rt.offsetMax = new Vector2(t, t);
                }
            }

            Color baseColor = Constants.HighlightColors.Snap;
            baseColor.a = Constants.HighlightSettings.SnapPulseMaxAlpha;
            img.color = baseColor;
            _activeSnapImage = img;

            _snapHighlightTween = img.DOFade(Constants.HighlightSettings.SnapPulseMinAlpha, Constants.HighlightSettings.SnapPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void StopSnapPulse()
        {
            if (_snapHighlightTween != null && _snapHighlightTween.IsActive())
            {
                _snapHighlightTween.Kill();
                _snapHighlightTween = null;
            }

            if (_activeSnapImage)
            {
                _activeSnapImage.color = Color.clear;
                _activeSnapImage = null;
            }
        }

        // ── 컴파일 결과 표시 (HighlightMode에 따라 외곽선 또는 블록 색상 틴트) ──
        private Tweener _compileHighlightTween;

        // 에러 — 완전 채도 빨간색으로 N회 깜빡인 뒤 원래 블록 색상으로 되돌아감
        public void ShowErrorHighlight() => PlayErrorBlink();

        // 성공 — 즉시 켜지지 않고 페이드인 (파도타기 연출 시 블록마다 시차를 두고 호출됨)
        public void ShowSuccessHighlight() => PlaySuccessFadeIn();

        public void ClearErrorHighlight()
        {
            StopCompileHighlightTween();
            ApplyCompileHighlight(Color.clear);
        }

        private void PlayErrorBlink()
        {
            StopCompileHighlightTween();

            Color error = Constants.HighlightColors.Error;
            Image target;
            Color from;

            if (Mode == HighlightMode.Tint)
            {
                SetHighlight(Outline, Color.clear);
                target = BodyImage;
                if (!target) return;
                CacheBodyOriginalColor();
                from = _bodyOriginalColor;
            }
            else
            {
                ResetBodyTint();
                target = Outline;
                if (!target) return;
                SetHighlightRect(target);
                from = Color.clear;
            }

            target.color = from;
            int toggles = Constants.HighlightSettings.ErrorBlinkCount * 2;
            _compileHighlightTween = target
                .DOColor(error, Constants.HighlightSettings.ErrorBlinkHalfDuration)
                .SetLoops(toggles, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject)
                .OnComplete(() => ApplyCompileHighlight(Color.clear)); // 깜빡임 종료 후 원래 색상으로 복원
        }

        private void PlaySuccessFadeIn()
        {
            StopCompileHighlightTween();

            Color success = Constants.HighlightColors.Success;
            float duration = Constants.HighlightSettings.SuccessWaveFadeInDuration;

            if (Mode == HighlightMode.Tint)
            {
                SetHighlight(Outline, Color.clear);
                Image body = BodyImage;
                if (!body) return;
                CacheBodyOriginalColor();
                Color target = Color.Lerp(_bodyOriginalColor, success, Constants.HighlightSettings.TintStrength);
                body.color = _bodyOriginalColor;
                _compileHighlightTween = body.DOColor(target, duration).SetEase(Ease.OutSine).SetLink(gameObject);
            }
            else
            {
                ResetBodyTint();
                Image outline = Outline;
                if (!outline) return;
                SetHighlightRect(outline);
                outline.color = Color.clear;
                _compileHighlightTween = outline.DOColor(success, duration).SetEase(Ease.OutSine).SetLink(gameObject);
            }
        }

        private void ApplyCompileHighlight(Color color)
        {
            if (Mode == HighlightMode.Tint)
            {
                SetHighlight(Outline, Color.clear);
                ApplyBodyTint(color);
            }
            else
            {
                ResetBodyTint();
                SetHighlight(Outline, color);
            }
        }

        private void StopCompileHighlightTween()
        {
            if (_compileHighlightTween != null && _compileHighlightTween.IsActive())
            {
                _compileHighlightTween.Kill();
                _compileHighlightTween = null;
            }
        }

        private Image ChainHighlight => GetOrFindHighlight(ref _chainHighlightImg, Constants.BlockParts.ChainHighlight);
        private Image ValueHighlight => GetOrFindHighlight(ref _valueHighlightImg, Constants.BlockParts.ValueHighlight);
        private Image Outline        => GetOrFindHighlight(ref _errorHighlightImg, Constants.BlockParts.Outline);

        // 블록 본체(스프라이트 또는 단색 Fill) — Sprite/Fill 둘 중 실제 존재하는 쪽을 찾는다
        private Image BodyImage
        {
            get
            {
                if (_bodyImg) return _bodyImg;
                foreach (Image img in GetComponentsInChildren<Image>(true))
                {
                    string n = img.gameObject.name;
                    if (n == Constants.BlockParts.Sprite || n == Constants.BlockParts.Fill || n == Constants.BlockParts.Background)
                    { _bodyImg = img; break; }
                }
                return _bodyImg;
            }
        }

        // BodyImage의 원래(하이라이트 적용 전) 색상을 최초 1회만 캐싱
        private void CacheBodyOriginalColor()
        {
            if (_bodyColorCached) return;
            Image body = BodyImage;
            if (!body) return;
            _bodyOriginalColor = body.color;
            _bodyColorCached = true;
        }

        // 원래 색상에서 target 쪽으로 살짝 섞음 (target이 clear면 원래 색상으로 복원)
        private void ApplyBodyTint(Color target)
        {
            Image body = BodyImage;
            if (!body) return;
            CacheBodyOriginalColor();

            body.color = target == Color.clear
                ? _bodyOriginalColor
                : Color.Lerp(_bodyOriginalColor, target, Constants.HighlightSettings.TintStrength);
        }

        private void ResetBodyTint()
        {
            if (_bodyColorCached && BodyImage)
                BodyImage.color = _bodyOriginalColor;
        }

        private static void SetHighlight(Image img, Color color)
        {
            if (!img) return;
            img.color = color;
            if (color != Color.clear) SetHighlightRect(img);
        }

        private static void SetHighlightRect(Image img)
        {
            float t = Constants.HighlightSettings.OutlineThickness;
            RectTransform rt = img.rectTransform;
            if (rt)
            {
                rt.offsetMin = new Vector2(-t, -t);
                rt.offsetMax = new Vector2(t, t);
            }
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
            StopSnapPulse();
            if (!_cg) TryGetComponent(out _cg);
            if (_cg) _cg.blocksRaycasts = true;
            IsDragHandled = false;
            ClearDragCache();
        }

        private void OnDestroy()
        {
            StopSnapPulse();
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

            // 드래그 시작 시 소켓이 확실히 부착되어 있도록 보장 (스냅 오프셋 기준점 정상화)
            BlockFactory.AttachSockets(this);

            _cachedChainOutSockets = FindObjectsOfType<ChainOutSocket>();
            _cachedInnerSockets = FindObjectsOfType<InnerSocket>();
            _cachedValueOutSockets = FindObjectsOfType<ValueOutSocket>();
            _cachedConditionOutSockets = FindObjectsOfType<ConditionOutSocket>();

            // 코딩을 다시 건드리기 시작하면 이전 빌드 결과(성공/에러 외곽선)는 더 이상 유효하지 않으므로 정리
            foreach (CodingBlock b in FindObjectsOfType<CodingBlock>())
                b.ClearErrorHighlight();

            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;
            _snapInnerSocket?.ClearSnapHighlight();
            _snapInnerSocket = null;

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

        // 값 계열(Value/Logic/Condition)은 가로 방향으로 붙고, 나머지는 세로 체인으로 붙는다
        private bool SnapsHorizontally =>
            Category is BlockCategory.Value or BlockCategory.Logic or BlockCategory.Condition;

        // Logic/Condition은 조건 체인(ConditionOut)에 먼저 붙어보고, 실패하면 값 슬롯(ValueOut)으로 넘어간다
        private bool PrefersConditionSocket =>
            Category is BlockCategory.Condition or BlockCategory.Logic;

        private void UpdateSnapHighlight()
        {
            CodingBlock newTarget = null;
            InnerSocket newInnerSocket = null;
            bool isValue = SnapsHorizontally;

            if (isValue)
            {
                // Condition / Logic: ConditionOut 스냅 우선, 없으면 ValueOut 스냅
                if (PrefersConditionSocket)
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
                InnerSocket innerSocket = FindSnapInnerSocket(out float innerSqr);

                // 두 범위가 겹치면 더 가까운 쪽 우선
                if (chainSocket && (!innerSocket || chainSqr <= innerSqr))
                {
                    newTarget = chainSocket.GetComponentInParent<CodingBlock>();
                }
                else if (innerSocket)
                {
                    newInnerSocket = innerSocket;
                }
            }

            if (newTarget == _snapTarget && newInnerSocket == _snapInnerSocket) return;

            _snapTarget?.ClearSnapHighlight();
            _snapInnerSocket?.ClearSnapHighlight();

            _snapTarget = newTarget;
            _snapInnerSocket = newInnerSocket;

            if (_snapInnerSocket)
            {
                _snapInnerSocket.ShowSnapHighlight();
            }
            else if (_snapTarget)
            {
                if (isValue) _snapTarget.ShowValueHighlight();
                else _snapTarget.ShowChainHighlight();
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            _snapTarget?.ClearSnapHighlight();
            _snapTarget = null;
            _snapInnerSocket?.ClearSnapHighlight();
            _snapInnerSocket = null;
            _cg.blocksRaycasts = true;
            IsDragHandled = false;

            if (TrySnapToSocket())
            {
                ClearDragCache();
                return;
            }

            if (!RootCanvas || transform.parent == RootCanvas.transform)
                ReturnHomeOrRelease(e);

            ClearDragCache();
        }

        /// <summary>
        /// 드롭 지점 주변에서 연결 가능한 소켓을 찾아 붙인다. 붙일 곳이 없으면 false.
        /// </summary>
        private bool TrySnapToSocket()
        {
            if (SnapsHorizontally)
            {
                // Condition / Logic: ConditionOut 스냅 우선
                if (PrefersConditionSocket)
                {
                    ConditionOutSocket condSlot = FindSnapConditionOutSocket();
                    if (condSlot) return AttachTo(condSlot.Accept);
                }

                ValueOutSocket slot = FindSnapValueOutSocket();
                if (slot) return AttachTo(slot.Accept);

                return false;
            }

            ChainOutSocket chainSocket = FindSnapOutSocket(out float chainSqr);
            InnerSocket innerSocket = FindSnapInnerSocket(out float innerSqr);

            // 두 후보가 모두 범위 안이면 더 가까운 쪽 우선
            if (chainSocket && (!innerSocket || chainSqr <= innerSqr))
                return AttachTo(chainSocket.Accept);

            if (innerSocket) return AttachTo(innerSocket.Accept);

            return false;
        }

        // 소켓 부착 공통 절차 — 드롭 처리 완료 표시 → 소켓 부착 → 대상 소켓에 인계
        private bool AttachTo(Action<CodingBlock> accept)
        {
            IsDragHandled = true;
            BlockFactory.AttachSockets(this);
            accept(this);
            return true;
        }

        // GetWorldCorners 인덱스 — 0:좌하 1:좌상 2:우상 3:우하
        private const int CornerBottomLeft = 0;
        private const int CornerTopLeft    = 1;
        private const int CornerTopRight   = 2;

        // GetWorldCorners 결과 재사용 버퍼 — 드래그 중 매 프레임 호출되므로 프레임당 할당을 피한다.
        // 값을 즉시 소비하고 메인 스레드에서만 쓰이므로 공유해도 안전하다.
        private readonly static Vector3[] _cornerBuffer = new Vector3[4];

        // RectTransform 월드 코너 두 지점의 중점 (변의 중앙)
        private static Vector2 EdgeCenter(RectTransform rt, int cornerA, int cornerB)
        {
            rt.GetWorldCorners(_cornerBuffer);
            return ((Vector2)_cornerBuffer[cornerA] + (Vector2)_cornerBuffer[cornerB]) * 0.5f;
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
                pos = EdgeCenter(_rt, CornerTopLeft, CornerTopRight);
                return true;
            }

            pos = default;
            return false;
        }

        // 가로 연결(값/조건) 기준점 — 해당 In 소켓 위치, 없으면 블록 좌측 중앙
        private bool TryGetHorizontalSnapOrigin<TInSocket>(out Vector2 pos) where TInSocket : Component
        {
            TInSocket inSocket = GetComponentInChildren<TInSocket>();
            if (inSocket)
            {
                pos = (Vector2)inSocket.transform.position;
                return true;
            }

            if (_rt)
            {
                pos = EdgeCenter(_rt, CornerBottomLeft, CornerTopLeft);
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
            if (!TryGetHorizontalSnapOrigin<ValueInSocket>(out Vector2 myPos)) return null;

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
                        && targetBlock.Category == BlockCategory.FlowControl && targetBlock.name.Contains(Constants.BlockLabels.RepeatKeyword))
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
            if (!TryGetHorizontalSnapOrigin<ConditionInSocket>(out Vector2 myPos)) return null;

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

        /// <summary>
        /// 이 블록에 물려 있는 모든 자식 블록을 소켓에서 떼어 인벤토리로 되돌린다 (하위까지 재귀).
        /// </summary>
        public void ReleaseAllAttachedChildren()
        {
            ReleaseAttachedChildren<InnerSocket>();
            ReleaseAttachedChildren<ValueOutSocket>();
            ReleaseAttachedChildren<ConditionOutSocket>();
            ReleaseAttachedChildren<ChainOutSocket>();
        }

        private void ReleaseAttachedChildren<TSocket>() where TSocket : BlockSocket
        {
            foreach (TSocket socket in GetComponentsInChildren<TSocket>(true))
            {
                CodingBlock child = socket.Occupant;
                if (!child) continue;

                socket.Release();
                child.ReleaseAllAttachedChildren();
                child.ReturnToInventory();
            }
        }

        /// <summary>
        /// 시작하기/완성하기 블록을 코딩 패널의 고정 자리(좌상단 / 좌하단)에 배치한다.
        /// 최초 스폰(BlockSpawner)과 인벤토리 반입 시 복귀(ReturnToInventory)가 같은 규칙을 쓰도록 공유한다.
        /// </summary>
        public static void ApplyControlBlockLayout(RectTransform rt, bool isStart, Transform codingZone)
        {
            if (!rt) return;

            rt.anchorMin = rt.anchorMax = new Vector2(0f, isStart ? 1f : 0f);
            float y = isStart
                ? -Constants.CodingZoneLayout.ControlBlockYInset
                : Constants.CodingZoneLayout.ControlBlockYInset;

            // 스크롤 콘텐츠가 뷰포트보다 클 때, 완성하기가 초기 화면(콘텐츠 좌상단 뷰) 안에 보이도록 보정
            if (!isStart && codingZone is RectTransform content)
            {
                ScrollRect scroll = content.GetComponentInParent<ScrollRect>();
                if (scroll && scroll.viewport)
                {
                    float overflow = content.rect.height - scroll.viewport.rect.height;
                    if (overflow > 0f) y += overflow;
                }
            }

            rt.anchoredPosition = new Vector2(Constants.CodingZoneLayout.ControlBlockX, y);
        }

        public static void ResetControlBlockPosition(CodingBlock block, Transform codingZone)
        {
            if (!block || !codingZone) return;

            block.transform.SetParent(codingZone, false);
            block.SetHome(codingZone);

            ApplyControlBlockLayout(block.transform as RectTransform,
                block.ControlRole == Data.ControlRole.Start, codingZone);

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