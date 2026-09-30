using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using TMPro;
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

        // 블록 시각 파츠 — 프리팹마다 깊이가 달라(일부는 Label 하위) 이름 탐색 대신 인스펙터로 연결한다.
        // 코드로 조립하는 블록은 BlockFactory가 SetParts로 넘겨준다.
        [Header("Parts")]
        [SerializeField] private Image outlineImage;
        [SerializeField] private Image chainHighlightImage;
        [SerializeField] private Image valueHighlightImage;
        [SerializeField] private Image bodyImage;
        [SerializeField] private TMP_Text labelText;
        [Tooltip("FlowControl 계열 전용 — else 분기를 덧붙일 때 맨 아래로 다시 보낼 푸터")]
        [SerializeField] private Transform footer;
        [Tooltip("프리팹에 미리 들어 있는 소켓(헤더 ValueOutSocket, InnerSocket 등) — Awake에서 소유 등록")]
        [SerializeField] private BlockSocket[] builtInSockets;

        // 레벨 5(함수) 한정 — 메인 체인(시작~완성)에는 함수 블록만 연결하도록 제한.
        // GameSceneManager가 레벨 로드 시 레이아웃에 함수 블록이 있으면 true로 설정.
        public static bool RestrictMainChainToFunction { get; set; }

        // 컴파일 성공/에러 표시 방식 — GameSceneManager가 레벨 로드 시 Inspector 설정값으로 초기화.
        public static HighlightMode Mode { get; set; } = HighlightMode.Outline;

        // 3_Game.json 튜닝 값 — GameSceneManager가 씬 로드 시 주입.
        // null이면(다른 씬에서 블록을 쓰거나 로드 전) 인스펙터·Constants 기본값으로 동작한다.
        public static Data.GameSceneSettings Settings { get; set; }

        // 스냅 반경은 배율 1 기준 튜닝 값이라, 코딩 패널을 확대/축소하면 소켓 간격과 함께 반경도 같은 비율로 맞춘다
        private float SnapRadius      => (Settings?.snapRadius        ?? _snapRadius) * CodingZoneZoom;
        private float ChainSnapRadius => (Settings?.chainSnapRadius   ?? _chainSnapRadius) * CodingZoneZoom;
        private float CodingZoneZoom  => _codingZone ? _codingZone.transform.localScale.x : 1f;
        private float SnapSeconds     => Settings?.blockSnapDuration ?? _snapSeconds;

        public BlockCategory Category { get; private set; }
        public ValueKind ValueKind { get; private set; }
        public Data.ControlRole ControlRole { get; private set; }
        public bool IsDragHandled { get; private set; }

        public Image OutlineImage => outlineImage;
        public Image ValueHighlightImage => valueHighlightImage;
        public TMP_Text LabelText => labelText;
        public Transform Footer => footer;

        private ILogger<CodingBlock> _logger;

        /// <summary>
        /// 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<CodingBlock> logger)
        {
            _logger = logger;
        }

        private Canvas _canvas;
        private RectTransform _rt;
        private CanvasGroup _cg;
        private Transform _homeParent;
        private int _homeIndex;
        private Vector2 _homeAnchoredPos;

        // 블록이 속한 씬의 코딩 패널/카테고리 존 — BlockSpawner가 생성 직후 SetZones로 넘겨준다
        private CodingZone _codingZone;
        private CategoryZone _categoryZone;

        public CodingZone CodingZone => _codingZone;
        public CategoryZone CategoryZone => _categoryZone;

        /// <summary>
        /// 블록이 속한 씬의 코딩 패널과 카테고리 존 참조를 받는다.
        /// </summary>
        public void SetZones(CodingZone codingZone, CategoryZone categoryZone)
        {
            _codingZone = codingZone;
            _categoryZone = categoryZone;
        }

        /// <summary>
        /// 코드로 조립한 블록의 시각 파츠를 연결한다 (프리팹 블록은 인스펙터로 연결됨).
        /// </summary>
        public void SetParts(Image outline, Image chainHighlight, Image valueHighlight, Image body)
        {
            outlineImage = outline;
            chainHighlightImage = chainHighlight;
            valueHighlightImage = valueHighlight;
            bodyImage = body;
        }

        // ── 소켓 소유 관계 ─────────────────────────────────────────
        // 이 블록에 직접 딸린 연결부 소켓 — 계층 탐색 대신 생성 시점에 등록해 둔다.
        // 등록 순서가 곧 배치 순서라(프리팹 내장 → 코드로 덧붙인 else 분기) 만약/아니면 Inner 순서가 보존된다.
        private readonly List<BlockSocket> _sockets = new();

        // 드래그 중 매 프레임 도는 판정(InnerSocket.CanAccept 등)이 리스트 할당 없이 인덱스로 순회하도록 노출한다
        public IReadOnlyList<BlockSocket> Sockets => _sockets;

        /// <summary>
        /// 프리팹에 들어 있던 소켓을 이 블록 소유로 등록한다.
        /// </summary>
        private void Awake()
        {
            if (builtInSockets == null) return;
            foreach (BlockSocket socket in builtInSockets)
                if (socket) RegisterSocket(socket);
        }

        /// <summary>
        /// 소켓을 이 블록 소유로 등록하고 소켓에도 소유 블록을 알려준다.
        /// </summary>
        public void RegisterSocket(BlockSocket socket)
        {
            if (!socket || _sockets.Contains(socket)) return;
            _sockets.Add(socket);
            socket.SetOwner(this);
        }

        /// <summary>
        /// 이 블록에 직접 딸린 지정 타입 소켓 중 첫 번째를 반환한다 (없으면 null).
        /// </summary>
        public T GetSocket<T>() where T : BlockSocket
        {
            foreach (BlockSocket socket in _sockets)
                if (socket is T typed && typed) return typed;
            return null;
        }

        /// <summary>
        /// 이 블록에 직접 딸린 지정 타입 소켓을 등록 순서대로 results에 추가한다.
        /// </summary>
        public void GetSockets<T>(List<T> results) where T : BlockSocket
        {
            foreach (BlockSocket socket in _sockets)
                if (socket is T typed && typed) results.Add(typed);
        }

        /// <summary>
        /// 이 블록이 (여러 단계를 거쳐서라도) FlowControl 내부 컨테이너에 들어가 있는지 소켓 관계를 따라 올라가며 확인한다.
        /// </summary>
        public bool IsInsideInnerContainer()
        {
            CodingBlock current = this;
            while (current)
            {
                Transform parent = current.transform.parent;
                if (!parent || !parent.TryGetComponent(out BlockSocket parentSocket)) return false;
                if (parentSocket is InnerSocket) return true;
                current = parentSocket.Owner;
            }
            return false;
        }

        // ── 스냅 후보 캐시 ─────────────────────────────────────────
        private readonly List<ChainOutSocket> _chainOutCandidates = new();
        private readonly List<InnerSocket> _innerCandidates = new();
        private readonly List<ValueOutSocket> _valueOutCandidates = new();
        private readonly List<ConditionOutSocket> _conditionOutCandidates = new();
        private bool _hasDragCache;

        /// <summary>
        /// 스냅 후보 소켓을 수집한다 — 후보는 코딩 패널 위 블록들의 소켓뿐이다.
        /// </summary>
        private void BuildDragCache()
        {
            ClearDragCache();
            _hasDragCache = true;

            if (!_codingZone)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 CodingZone이 연결되지 않아 스냅 대상을 찾을 수 없습니다.");
                return;
            }

            foreach (CodingBlock block in _codingZone.Blocks)
            {
                if (!block || !block.gameObject.activeInHierarchy || !_codingZone.Contains(block)) continue;

                block.GetSockets(_chainOutCandidates);
                block.GetSockets(_innerCandidates);
                block.GetSockets(_valueOutCandidates);
                block.GetSockets(_conditionOutCandidates);
            }
        }

        /// <summary>
        /// 드래그 도중이 아닌데 스냅 판정이 호출된 경우(OnBeginDrag가 조기 반환 등)에도 후보를 준비한다.
        /// </summary>
        private void EnsureDragCache()
        {
            if (!_hasDragCache) BuildDragCache();
        }

        /// <summary>
        /// 코딩 패널과 인벤토리의 모든 블록에서 컴파일 결과 표시(성공/에러)를 지운다.
        /// </summary>
        public static void ClearAllErrorHighlights(CodingZone codingZone)
        {
            if (!codingZone) return;

            foreach (CodingBlock b in codingZone.Blocks)
                if (b) b.ClearErrorHighlight();
        }

        /// <summary>
        /// 드래그 동안 모아 둔 스냅 후보를 비운다.
        /// </summary>
        private void ClearDragCache()
        {
            _chainOutCandidates.Clear();
            _innerCandidates.Clear();
            _valueOutCandidates.Clear();
            _conditionOutCandidates.Clear();
            _hasDragCache = false;
        }

        // ── 하이라이트 ─────────────────────────────────────────────
        private CodingBlock _snapTarget;
        private InnerSocket _snapInnerSocket;
        private Color _bodyOriginalColor;
        private bool _bodyColorCached;

        private Tweener _snapHighlightTween;
        private Image _activeSnapImage;

        // ── 스냅 하이라이트 (드래그 중 연결 가능 지점 표시 — 부드러운 깜빡임 펄스 연출) ──

        /// <summary>
        /// 하단 체인 연결 지점에 스냅 하이라이트 펄스를 켠다.
        /// </summary>
        public void ShowChainHighlight() => PlaySnapPulse(chainHighlightImage, isVerticalChain: true);

        /// <summary>
        /// 우측 값 연결 지점에 스냅 하이라이트 펄스를 켠다.
        /// </summary>
        public void ShowValueHighlight() => PlaySnapPulse(valueHighlightImage, isVerticalChain: false);

        /// <summary>
        /// 스냅 하이라이트 펄스를 멈추고 체인·값 하이라이트를 모두 끈다.
        /// </summary>
        public void ClearSnapHighlight()
        {
            StopSnapPulse();
            SetHighlight(chainHighlightImage, Color.clear);
            SetHighlight(valueHighlightImage, Color.clear);
        }

        // 만약 블록 — 반복하기와 같은 FlowControl이라 이름으로 구분한다 (조건 스냅 하이라이트 전용 처리).
        // 런타임에는 BlockFactory가 이름을 라벨("만약")로 바꾸지만, 에디터 테스트 씬에 프리팹 원본을
        // 그대로 배치하면 이름이 "IfBlock"이므로 프리팹 키도 함께 본다.
        private bool IsIfBlock => Category == BlockCategory.FlowControl
            && (name.Contains(Constants.BlockLabels.If) || name.Contains(Constants.BlockAssets.IfPrefab));

        /// <summary>
        /// 지정한 하이라이트 이미지를 연결 방향에 맞게 넓힌 뒤 무한 반복 알파 펄스를 재생한다.
        /// </summary>
        private void PlaySnapPulse(Image img, bool isVerticalChain = false)
        {
            if (!img)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 스냅 하이라이트 이미지가 연결되지 않았습니다.");
                return;
            }
            if (_activeSnapImage == img && _snapHighlightTween != null && _snapHighlightTween.IsActive()) return;

            StopSnapPulse();

            float t = Constants.HighlightSettings.OutlineThickness;
            RectTransform rt = img.rectTransform;
            if (isVerticalChain)
            {
                // ChainHighlight: 좌우(X) 0, 상하(Y) -10~10 확장
                rt.offsetMin = new Vector2(0f, -t);
                rt.offsetMax = new Vector2(0f, t);
            }
            else if (IsIfBlock)
            {
                // 만약 블록의 조건 슬롯: C자 본체로 초록이 흘러내리지 않도록 상하는 확장하지 않고 좌우만 넓힌다.
                // 세로 범위는 IfBlock.prefab의 ValueHighlight에 붙은 BlockOutlineIfValue 머티리얼이
                // 헤더 높이(UV Y 0.5~1)로 잘라낸다 — 값은 인스펙터에서 조정한다.
                rt.offsetMin = new Vector2(-t, 0f);
                rt.offsetMax = new Vector2(t, 0f);
            }
            else
            {
                rt.offsetMin = new Vector2(-t, -t);
                rt.offsetMax = new Vector2(t, t);
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

        /// <summary>
        /// 진행 중인 스냅 펄스 트윈을 멈추고 해당 이미지를 투명하게 되돌린다.
        /// </summary>
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

        /// <summary>
        /// 컴파일 에러 표시 — 완전 채도 빨간색으로 N회 깜빡인 뒤 원래 블록 색상으로 되돌아간다.
        /// </summary>
        public void ShowErrorHighlight() => PlayErrorBlink();

        /// <summary>
        /// 컴파일 성공 표시 — 즉시 켜지지 않고 페이드인한다 (파도타기 연출 시 블록마다 시차를 두고 호출됨).
        /// </summary>
        public void ShowSuccessHighlight() => PlaySuccessFadeIn();

        /// <summary>
        /// 진행 중인 컴파일 결과 연출을 멈추고 성공·에러 표시를 지운다.
        /// </summary>
        public void ClearErrorHighlight()
        {
            StopCompileHighlightTween();
            ApplyCompileHighlight(Color.clear);
        }

        /// <summary>
        /// HighlightMode에 맞는 대상(외곽선 또는 본체)에 빨간색 깜빡임을 재생한다.
        /// </summary>
        private void PlayErrorBlink()
        {
            StopCompileHighlightTween();

            Color error = Constants.HighlightColors.Error;
            Image target;
            Color from;

            if (Mode == HighlightMode.Tint)
            {
                SetHighlight(outlineImage, Color.clear);
                target = bodyImage;
                if (!target)
                {
                    if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 본체 이미지가 연결되지 않아 에러 표시를 건너뜁니다.");
                    return;
                }
                CacheBodyOriginalColor();
                from = _bodyOriginalColor;
            }
            else
            {
                ResetBodyTint();
                target = outlineImage;
                if (!target)
                {
                    if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 외곽선 이미지가 연결되지 않아 에러 표시를 건너뜁니다.");
                    return;
                }
                SetHighlightRect(target);
                from = Color.clear;
            }

            target.color = from;
            int toggles = (Settings?.errorBlinkCount ?? Constants.HighlightSettings.ErrorBlinkCount) * 2;
            _compileHighlightTween = target
                .DOColor(error, Settings?.errorBlinkHalfDuration ?? Constants.HighlightSettings.ErrorBlinkHalfDuration)
                .SetLoops(toggles, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject)
                .OnComplete(() => ApplyCompileHighlight(Color.clear)); // 깜빡임 종료 후 원래 색상으로 복원
        }

        /// <summary>
        /// HighlightMode에 맞는 대상(외곽선 또는 본체)에 초록색 성공 표시를 페이드인한다.
        /// </summary>
        private void PlaySuccessFadeIn()
        {
            StopCompileHighlightTween();

            Color success = Constants.HighlightColors.Success;
            float duration = Settings?.successWaveFadeInDuration ?? Constants.HighlightSettings.SuccessWaveFadeInDuration;

            if (Mode == HighlightMode.Tint)
            {
                SetHighlight(outlineImage, Color.clear);
                if (!bodyImage)
                {
                    if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 본체 이미지가 연결되지 않아 성공 표시를 건너뜁니다.");
                    return;
                }
                CacheBodyOriginalColor();
                Color target = Color.Lerp(_bodyOriginalColor, success, Constants.HighlightSettings.TintStrength);
                bodyImage.color = _bodyOriginalColor;
                _compileHighlightTween = bodyImage.DOColor(target, duration).SetEase(Ease.OutSine).SetLink(gameObject);
            }
            else
            {
                ResetBodyTint();
                if (!outlineImage)
                {
                    if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 외곽선 이미지가 연결되지 않아 성공 표시를 건너뜁니다.");
                    return;
                }
                SetHighlightRect(outlineImage);
                outlineImage.color = Color.clear;
                _compileHighlightTween = outlineImage.DOColor(success, duration).SetEase(Ease.OutSine).SetLink(gameObject);
            }
        }

        /// <summary>
        /// HighlightMode에 맞게 외곽선 색 또는 본체 틴트를 즉시 적용한다 (clear면 원래 상태로 복원).
        /// </summary>
        private void ApplyCompileHighlight(Color color)
        {
            if (Mode == HighlightMode.Tint)
            {
                SetHighlight(outlineImage, Color.clear);
                ApplyBodyTint(color);
            }
            else
            {
                ResetBodyTint();
                SetHighlight(outlineImage, color);
            }
        }

        /// <summary>
        /// 진행 중인 컴파일 결과 트윈을 멈춘다.
        /// </summary>
        private void StopCompileHighlightTween()
        {
            if (_compileHighlightTween != null && _compileHighlightTween.IsActive())
            {
                _compileHighlightTween.Kill();
                _compileHighlightTween = null;
            }
        }

        /// <summary>
        /// 본체 이미지의 원래(하이라이트 적용 전) 색상을 최초 1회만 캐싱한다.
        /// </summary>
        private void CacheBodyOriginalColor()
        {
            if (_bodyColorCached || !bodyImage) return;
            _bodyOriginalColor = bodyImage.color;
            _bodyColorCached = true;
        }

        /// <summary>
        /// 원래 색상에서 target 쪽으로 살짝 섞는다 (target이 clear면 원래 색상으로 복원).
        /// </summary>
        private void ApplyBodyTint(Color target)
        {
            if (!bodyImage) return;
            CacheBodyOriginalColor();

            bodyImage.color = target == Color.clear
                ? _bodyOriginalColor
                : Color.Lerp(_bodyOriginalColor, target, Constants.HighlightSettings.TintStrength);
        }

        /// <summary>
        /// 틴트가 적용된 적이 있으면 본체 색을 원래대로 되돌린다.
        /// </summary>
        private void ResetBodyTint()
        {
            if (_bodyColorCached && bodyImage)
                bodyImage.color = _bodyOriginalColor;
        }

        /// <summary>
        /// 하이라이트 이미지 색을 바꾸고, 켜는 경우 외곽선 두께만큼 영역을 넓힌다.
        /// </summary>
        private static void SetHighlight(Image img, Color color)
        {
            if (!img) return;
            img.color = color;
            if (color != Color.clear) SetHighlightRect(img);
        }

        /// <summary>
        /// 하이라이트 이미지를 블록 사방으로 외곽선 두께만큼 넓힌다.
        /// </summary>
        private static void SetHighlightRect(Image img)
        {
            float t = Constants.HighlightSettings.OutlineThickness;
            RectTransform rt = img.rectTransform;
            rt.offsetMin = new Vector2(-t, -t);
            rt.offsetMax = new Vector2(t, t);
        }

        /// <summary>
        /// 블록 메타(카테고리·값 타입·제어 역할)와 드래그 기준 캔버스를 설정한다.
        /// </summary>
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

        /// <summary>
        /// 비활성화될 때 펄스를 멈추고 드래그 상태를 초기화한다.
        /// </summary>
        private void OnDisable()
        {
            StopSnapPulse();
            if (!_cg) TryGetComponent(out _cg);
            if (_cg) _cg.blocksRaycasts = true;
            IsDragHandled = false;
            ClearDragCache();
        }

        /// <summary>
        /// 파괴될 때 스냅 펄스 트윈을 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            StopSnapPulse();
        }

        /// <summary>
        /// 드래그를 시작하면 스냅 후보를 모으고, 원래 자리(소켓)에서 떼어 루트 캔버스 최상단으로 옮긴다.
        /// </summary>
        public void OnBeginDrag(PointerEventData e)
        {
            if (!_canvas)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 루트 캔버스가 지정되지 않아 드래그할 수 없습니다.");
                return;
            }

            // 드래그 시작 시 소켓이 확실히 부착되어 있도록 보장 (스냅 오프셋 기준점 정상화)
            BlockFactory.AttachSockets(this);

            BuildDragCache();

            // 코딩을 다시 건드리기 시작하면 이전 빌드 결과(성공/에러 외곽선)는 더 이상 유효하지 않으므로 정리
            ClearAllErrorHighlights(_codingZone);

            if (_snapTarget) _snapTarget.ClearSnapHighlight();
            _snapTarget = null;
            if (_snapInnerSocket) _snapInnerSocket.ClearSnapHighlight();
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

            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();
            _cg.blocksRaycasts = false;
        }

        /// <summary>
        /// 체인 중간에서 떼어낼 때, 이 블록 아래에 붙어 있던 블록을 원래 부모 소켓에 이어 붙인다.
        /// </summary>
        private void SpliceOutChild(Action<CodingBlock> acceptToParent)
        {
            ChainOutSocket myOut = GetSocket<ChainOutSocket>();
            CodingBlock myChild = myOut ? myOut.Occupant : null;
            if (!myChild) return;

            myOut.Release();
            acceptToParent(myChild);
        }

        /// <summary>
        /// 포인터 이동량만큼 블록을 옮기고 스냅 하이라이트를 갱신한다.
        /// </summary>
        public void OnDrag(PointerEventData e)
        {
            if (!_canvas) return;

            _rt.anchoredPosition += e.delta / _canvas.scaleFactor;
            UpdateSnapHighlight();
        }

        /// <summary>
        /// 부모가 바뀌면 크기를 부모 기준 1로 맞춘다 — 확대/축소된 코딩 패널과 인벤토리를 오가도 블록이 놓인 곳의 배율을 따르게 한다.
        /// 드래그 중(루트 캔버스 직속)에는 들어 올리기 전에 보이던 크기를 그대로 유지한다.
        /// </summary>
        private void OnTransformParentChanged()
        {
            if (_canvas && transform.parent == _canvas.transform) return;
            transform.localScale = Vector3.one;
        }

        // 값 계열(Value/Logic/Condition)은 가로 방향으로 붙고, 나머지는 세로 체인으로 붙는다
        private bool SnapsHorizontally =>
            Category is BlockCategory.Value or BlockCategory.Logic or BlockCategory.Condition;

        // Logic/Condition은 조건 체인(ConditionOut)에 먼저 붙어보고, 실패하면 값 슬롯(ValueOut)으로 넘어간다
        private bool PrefersConditionSocket =>
            Category is BlockCategory.Condition or BlockCategory.Logic;

        /// <summary>
        /// 현재 위치에서 스냅될 대상을 찾아, 바뀐 경우에만 이전 하이라이트를 끄고 새 대상에 켠다.
        /// </summary>
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
                        newTarget = condSocket.Owner;
                }

                if (!newTarget)
                {
                    ValueOutSocket socket = FindSnapValueOutSocket();
                    if (socket)
                        newTarget = socket.Owner;
                }
            }
            else
            {
                ChainOutSocket chainSocket = FindSnapOutSocket(out float chainSqr);
                InnerSocket innerSocket = FindSnapInnerSocket(out float innerSqr);

                // 두 범위가 겹치면 더 가까운 쪽 우선
                if (chainSocket && (!innerSocket || chainSqr <= innerSqr))
                {
                    newTarget = chainSocket.Owner;
                }
                else if (innerSocket)
                {
                    newInnerSocket = innerSocket;
                }
            }

            if (newTarget == _snapTarget && newInnerSocket == _snapInnerSocket) return;

            if (_snapTarget) _snapTarget.ClearSnapHighlight();
            if (_snapInnerSocket) _snapInnerSocket.ClearSnapHighlight();

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

        /// <summary>
        /// 드래그를 끝내면 가까운 소켓에 붙이고, 붙일 곳이 없으면 코딩 패널에 놓거나 인벤토리로 되돌린다.
        /// </summary>
        public void OnEndDrag(PointerEventData e)
        {
            if (_snapTarget) _snapTarget.ClearSnapHighlight();
            _snapTarget = null;
            if (_snapInnerSocket) _snapInnerSocket.ClearSnapHighlight();
            _snapInnerSocket = null;
            _cg.blocksRaycasts = true;
            IsDragHandled = false;

            if (TrySnapToSocket())
            {
                ClearDragCache();
                return;
            }

            if (!_canvas || transform.parent == _canvas.transform)
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

        /// <summary>
        /// 소켓 부착 공통 절차 — 드롭 처리 완료 표시 → 소켓 부착 → 대상 소켓에 인계.
        /// </summary>
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

        /// <summary>
        /// RectTransform 월드 코너 두 지점의 중점(변의 중앙)을 반환한다.
        /// </summary>
        private static Vector2 EdgeCenter(RectTransform rt, int cornerA, int cornerB)
        {
            rt.GetWorldCorners(_cornerBuffer);
            return ((Vector2)_cornerBuffer[cornerA] + (Vector2)_cornerBuffer[cornerB]) * 0.5f;
        }

        /// <summary>
        /// ChainInSocket 위치 또는 블록 상단 중앙을 체인 스냅 기준점으로 반환한다.
        /// </summary>
        private bool TryGetChainSnapOrigin(out Vector2 pos)
        {
            ChainInSocket inSocket = BlockSocket.FindChildComponent<ChainInSocket>(transform, Constants.Sockets.ChainInName);
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

        /// <summary>
        /// 가로 연결(값/조건) 기준점으로 해당 In 소켓 위치를, 없으면 블록 좌측 중앙을 반환한다.
        /// </summary>
        private bool TryGetHorizontalSnapOrigin<TInSocket>(string socketName, out Vector2 pos) where TInSocket : Component
        {
            TInSocket inSocket = BlockSocket.FindChildComponent<TInSocket>(transform, socketName);
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

        /// <summary>
        /// 이 블록의 체인 기준점 아래쪽 반경 안에서 가장 가까운 ChainOutSocket을 찾는다
        /// (ChainInSocket이 없는 인벤토리 블록은 블록 상단 중앙을 기준점으로 사용).
        /// </summary>
        private ChainOutSocket FindSnapOutSocket(out float bestSqr)
        {
            if (!TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            ChainOutSocket best = null;
            float minSqr = ChainSnapRadius * ChainSnapRadius;

            EnsureDragCache();
            foreach (ChainOutSocket candidate in _chainOutCandidates)
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.CanAccept(this)) continue;

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

        /// <summary>
        /// 방향 제한 없이 반경 안에서 가장 가까운 InnerSocket(FlowControl 내부 진입)을 찾는다.
        /// </summary>
        private InnerSocket FindSnapInnerSocket(out float bestSqr)
        {
            if (Category == BlockCategory.Control || !TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            InnerSocket best = null;
            float minSqr = ChainSnapRadius * ChainSnapRadius;

            EnsureDragCache();
            foreach (InnerSocket candidate in _innerCandidates)
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.CanAccept(this)) continue;

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
        /// 허용된 블록 조합 규칙에 맞는 우측 값 연결부(ValueOutSocket)를 찾는다.
        /// </summary>
        private ValueOutSocket FindSnapValueOutSocket()
        {
            if (!TryGetHorizontalSnapOrigin<ValueInSocket>(Constants.Sockets.ValueInName, out Vector2 myPos)) return null;

            ValueOutSocket best = null;
            float minSqr = SnapRadius * SnapRadius;

            EnsureDragCache();
            foreach (ValueOutSocket candidate in _valueOutCandidates)
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;

                CodingBlock targetBlock = candidate.Owner;
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

        /// <summary>
        /// ConditionInSocket 기준으로 가장 가까운 ConditionOutSocket을 찾는다.
        /// </summary>
        private ConditionOutSocket FindSnapConditionOutSocket()
        {
            if (!TryGetHorizontalSnapOrigin<ConditionInSocket>(Constants.Sockets.ConditionInName, out Vector2 myPos)) return null;

            ConditionOutSocket best = null;
            float minSqr = SnapRadius * SnapRadius;

            EnsureDragCache();
            foreach (ConditionOutSocket candidate in _conditionOutCandidates)
            {
                if (candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsEmpty) continue;

                CodingBlock targetBlock = candidate.Owner;
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

        /// <summary>
        /// 소켓의 자식으로 옮긴 뒤 OutBack 이징으로 목표 오프셋까지 미끄러지듯 붙인다.
        /// </summary>
        public async UniTaskVoid SnapInto(Transform socket, Vector2 targetOffset = default)
        {
            transform.SetParent(socket, true);
            _homeParent = socket;

            try
            {
                // OutBack 이징으로 스냅 손맛 부여, 드래그 등으로 부모가 바뀌면 트윈 중단
                Tween tween = null;
                tween = _rt.DOAnchorPos(targetOffset, SnapSeconds)
                    .SetEase(Ease.OutBack)
                    .OnUpdate(() =>
                    {
                        if (transform.parent != socket) tween.Kill();
                    });
                await tween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (_logger != null) _logger.ZLogError(ex, $"[CodingBlock] SnapInto failed on {name}");
                return;
            }

            if (!this || transform.parent != socket) return;

            _rt.anchoredPosition = targetOffset;
        }

        /// <summary>
        /// 지정한 부모의 원점에 즉시 배치하고 그곳을 복귀 위치로 기록한다.
        /// </summary>
        public void PlaceIn(Transform parent)
        {
            transform.SetParent(parent, false);
            _rt.anchoredPosition = Vector2.zero;
            _homeParent = parent;
        }

        private Transform _inventoryParent;

        /// <summary>
        /// 인벤토리로 되돌아갈 때 들어갈 부모(인벤토리 Content)를 기록한다.
        /// </summary>
        public void SetInventoryHome(Transform invParent)
        {
            _inventoryParent = invParent;
        }

        public Transform InventoryParent
        {
            get
            {
                if (!_inventoryParent && _categoryZone)
                    _inventoryParent = _categoryZone.InventoryContent;
                return _inventoryParent;
            }
        }

        /// <summary>
        /// 드래그 취소 시 되돌아갈 부모를 기록한다.
        /// </summary>
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

        // 호출마다 리스트를 새로 만들지 않도록 재사용한다 — 재귀(자식 블록 해제)는 다른 블록 인스턴스에서 일어나 버퍼가 겹치지 않는다
        private readonly List<BlockSocket> _releaseBuffer = new();

        /// <summary>
        /// 이 블록에 직접 딸린 지정 타입 소켓의 점유 블록을 떼어 인벤토리로 되돌린다.
        /// </summary>
        private void ReleaseAttachedChildren<TSocket>() where TSocket : BlockSocket
        {
            _releaseBuffer.Clear();
            foreach (BlockSocket socket in _sockets)
                if (socket is TSocket && socket) _releaseBuffer.Add(socket);

            foreach (BlockSocket socket in _releaseBuffer)
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
        public static void ApplyControlBlockLayout(RectTransform rt, bool isStart, CodingZone codingZone)
        {
            if (!rt) return;

            rt.anchorMin = rt.anchorMax = new Vector2(0f, isStart ? 1f : 0f);
            float y = isStart
                ? -Constants.CodingZoneLayout.ControlBlockYInset
                : Constants.CodingZoneLayout.ControlBlockYInset;

            // 스크롤 콘텐츠가 뷰포트보다 클 때, 완성하기가 초기 화면(콘텐츠 좌상단 뷰) 안에 보이도록 보정
            if (!isStart && codingZone && codingZone.transform is RectTransform content)
            {
                ScrollRect scroll = codingZone.ScrollRect;
                if (scroll && scroll.viewport)
                {
                    // 확대/축소 중이면 뷰포트에 보이는 콘텐츠 높이가 배율만큼 달라진다
                    float overflow = content.rect.height - scroll.viewport.rect.height / content.localScale.y;
                    if (overflow > 0f) y += overflow;
                }
            }

            rt.anchoredPosition = new Vector2(Constants.CodingZoneLayout.ControlBlockX, y);
        }

        /// <summary>
        /// 시작하기/완성하기 블록을 코딩 패널 직속의 고정 자리로 되돌린다.
        /// </summary>
        public static void ResetControlBlockPosition(CodingBlock block, CodingZone codingZone)
        {
            if (!block || !codingZone) return;

            block.transform.SetParent(codingZone.transform, false);
            block.SetHome(codingZone.transform);

            ApplyControlBlockLayout(block.transform as RectTransform,
                block.ControlRole == Data.ControlRole.Start, codingZone);

            block.gameObject.SetActive(true);
        }

        /// <summary>
        /// 붙어 있던 자식 블록까지 모두 떼어 인벤토리의 현재 탭으로 되돌린다 (제어 블록은 코딩 패널 고정 자리로).
        /// </summary>
        public void ReturnToInventory()
        {
            if (Category == BlockCategory.Control)
            {
                if (_codingZone)
                    ResetControlBlockPosition(this, _codingZone);
                else if (_logger != null)
                    _logger.ZLogWarning($"[CodingBlock] {name}에 CodingZone이 연결되지 않아 제자리로 되돌릴 수 없습니다.");
                return;
            }

            ReleaseAllAttachedChildren();

            Transform targetParent = InventoryParent;
            if (targetParent)
            {
                transform.SetParent(targetParent, false);
                SetHome(targetParent);

                if (_categoryZone)
                    gameObject.SetActive(BlockFactory.GetTabCategory(Category) == _categoryZone.CurrentCategory);
                else if (_logger != null)
                    _logger.ZLogWarning($"[CodingBlock] {name}에 CategoryZone이 연결되지 않아 탭 필터를 적용하지 못했습니다.");
            }
            else if (_homeParent)
            {
                transform.SetParent(_homeParent, false);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[CodingBlock] {name}의 인벤토리·복귀 위치가 모두 없어 제자리에 둡니다.");
            }
        }

        /// <summary>
        /// 드래그 시작 전 위치(부모·순서·좌표)로 되돌리고, 떼어냈던 소켓의 점유를 복구한다.
        /// </summary>
        public void ReturnHome()
        {
            if (!_homeParent) return;

            bool isReturningToInventory = !_codingZone || !_homeParent.IsChildOf(_codingZone.transform);
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
        /// 포인터 위치가 코딩 영역 내부인지 판별하여 코딩 패널에 놓거나 인벤토리로 되돌린다.
        /// </summary>
        private void ReturnHomeOrRelease(PointerEventData e)
        {
            CodingZone zone = _codingZone;
            if (!zone)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] CodingZone이 연결되지 않았습니다.");
                ReturnToInventory();
                return;
            }

            if (!zone.TryGetComponent(out RectTransform zoneRect))
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] CodingZone 영역에 RectTransform이 없습니다.");
                ReturnToInventory();
                return;
            }

            // 스크롤 존이면 확대된 Content가 아니라 화면에 보이는 Viewport 기준으로 내부 판정
            ScrollRect scroll = zone.ScrollRect;
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
        /// <summary>
        /// 선택된 블록의 스냅 반경을 씬 뷰에 표시한다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_rt) TryGetComponent<RectTransform>(out _rt);
            RectTransform rt = _rt;
            if (!rt) return;

            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(rt.position, SnapRadius);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(rt.position, 4f);

            Handles.Label(rt.position + Vector3.up * (SnapRadius + 10f),
                $"snap r={SnapRadius}",
                new GUIStyle { normal = { textColor = new Color(0.3f, 0.7f, 1f) }, fontSize = 9 });
        }
#endif
    }
}
