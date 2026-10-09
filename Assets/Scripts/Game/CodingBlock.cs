using System;
using System.Collections.Generic;
using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
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

        // 컴파일 성공/에러 표시 방식 — GameSceneManager가 레벨 로드 시 Inspector 설정값으로 초기화.
        public static HighlightMode Mode { get; set; } = HighlightMode.Outline;

        // 3_Game.json 튜닝 값 — GameSceneManager가 씬 로드 시 주입.
        // null이면(다른 씬에서 블록을 쓰거나 로드 전) 인스펙터·Constants 기본값으로 동작한다.
        public static Data.GameSceneSettings Settings { get; set; }

        // 스냅 반경은 배율 1 기준 튜닝 값이라, 코딩 패널을 확대/축소하면 소켓 간격과 함께 반경도 같은 비율로 맞춘다
        private float SnapRadius      => (Settings?.snapRadius        ?? _snapRadius) * ZoneZoom;
        private float ChainSnapRadius => (Settings?.chainSnapRadius   ?? _chainSnapRadius) * ZoneZoom;
        private float ZoneZoom        => _codingZone ? _codingZone.transform.localScale.x : 1f;
        private float SnapSeconds     => Settings?.blockSnapDuration ?? _snapSeconds;

        public BlockCategory Category { get; private set; }
        public ValueKind ValueKind { get; private set; }
        public Data.ControlRole ControlRole { get; private set; }

        // BlockFactory.AttachSockets가 연결부 소켓을 이미 붙였는지 — 드래그마다 소켓을 다시 찾지 않도록 한 번만 붙인다
        public bool SocketsAttached { get; private set; }

        /// <summary>
        /// 연결부 소켓을 모두 붙였다고 표시한다 (BlockFactory.AttachSockets에서만 호출).
        /// </summary>
        public void MarkSocketsAttached() => SocketsAttached = true;

        // 반복하기 블록인지 — 블록 이름(라벨)으로 판별하되, name 조회는 매번 문자열을 새로 할당하므로
        // 드래그 중 매 이벤트 도는 스냅 탐색을 위해 처음 판별한 값을 재사용한다(이름은 생성 시 한 번만 정해짐)
        private bool? _isRepeat;
        public bool IsRepeat => _isRepeat ??= Category == BlockCategory.FlowControl
            && name.Contains(Constants.BlockLabels.RepeatKeyword);

        // 핀치 때문에 취소된 드래그 — 손가락을 뗄 때 오는 OnDrop/OnEndDrag가 블록을 옮기지 않도록 드롭 처리 쪽에서도 확인한다
        public bool IsDragCancelled { get; private set; }

        // 지금 집어 든 블록들 — 핀치가 시작되면 모두 드래그 전 자리로 되돌린다 (여러 손가락이 각각 블록을 들 수 있음)
        private readonly static HashSet<CodingBlock> _activeDrags = new();
        private readonly static List<CodingBlock> _cancelBuffer = new();
        private bool _isDragging;

        // 이 블록을 끌고 있는 손가락(포인터) — 두 손가락이 같은 블록을 동시에 잡으면 손가락마다 OnBeginDrag가 오므로 처음 잡은 손가락만 따른다.
        // 둘 다 따르면 한 손가락이 소켓에 붙인 블록을 다른 손가락이 계속 끌어 소켓 기록과 실제 자리가 어긋나고, 최악에는 블록이 자기 아래에 붙어 순환한다
        private const int NoDragPointer = int.MinValue;
        private int _dragPointerId = NoDragPointer;

        /// <summary>
        /// 이 포인터 이벤트가 지금 이 블록을 끄는 손가락의 것인지 확인한다(드롭 영역은 다른 손가락의 놓기로 블록을 옮기지 않는다).
        /// </summary>
        public bool IsDragPointer(PointerEventData e) => _dragPointerId != NoDragPointer && e.pointerId == _dragPointerId;

        public Image OutlineImage => outlineImage;
        public Image ValueHighlightImage => valueHighlightImage;
        public TMP_Text LabelText => labelText;
        public Transform Footer => footer;

        private ILogger<CodingBlock> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거, 사운드 매니저, 체험자 정보를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<CodingBlock> logger, SoundManager soundManager, VisitorInfoProvider visitorInfoProvider)
        {
            _logger = logger;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
        }

        private VisitorInfoProvider _visitorInfoProvider; // 행동 로그 주어

        // 행동 로그용 — 드래그를 시작할 때 떼어 낸 소켓 자리(예: "'시작하기' 아래"), 소켓에 붙어 있지 않았으면 null
        private string _dragFromSocket;

        // 드래그를 시작할 때 떼어 낸 소켓 — 같은 소켓에 다시 붙였는지 판단한다(붙는 순간 _homeParent는 새 소켓으로 바뀐다)
        private BlockSocket _dragOriginSocket;

        // 행동 로그용 — 드래그를 시작할 때 블록 목록(인벤토리)에 있었는지
        private bool _dragFromInventory;

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

        // 이 블록의 진입 소켓(스냅 기준점) — 드래그 중 매 이벤트 이름으로 자식을 찾지 않도록 후보와 함께 모아 둔다
        private ChainInSocket _chainInSocket;
        private ValueInSocket _valueInSocket;
        private ConditionInSocket _conditionInSocket;

        /// <summary>
        /// 스냅 후보 소켓을 수집한다 — 후보는 코딩 패널 위 블록들의 소켓뿐이다.
        /// </summary>
        private void BuildDragCache()
        {
            ClearDragCache();
            _hasDragCache = true;

            _chainInSocket = BlockSocket.FindChildComponent<ChainInSocket>(transform, Constants.Sockets.ChainInName);
            _valueInSocket = BlockSocket.FindChildComponent<ValueInSocket>(transform, Constants.Sockets.ValueInName);
            _conditionInSocket = BlockSocket.FindChildComponent<ConditionInSocket>(transform, Constants.Sockets.ConditionInName);

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
            _chainInSocket = null;
            _valueInSocket = null;
            _conditionInSocket = null;
            _hasDragCache = false;
        }

        // ── 하이라이트 ─────────────────────────────────────────────
        private CodingBlock _snapTarget;
        private InnerSocket _snapInnerSocket;

        // 드래그 중 마지막으로 하이라이트한 소켓 — 손을 떼면 이 소켓에 붙는다
        private BlockSocket _highlightedSocket;

        // 드래그 중 붙을 소켓이 하이라이트돼 있는지 — 드롭 영역은 이때 블록을 옮기지 않고 OnEndDrag의 부착에 맡긴다
        public bool HasSnapTarget => _highlightedSocket;
        private Color _bodyOriginalColor;
        private bool _bodyColorCached;

        private Tweener _snapHighlightTween;
        private Image _activeSnapImage;

        // ── 스냅 하이라이트 (드래그 중 연결 가능 지점 표시 — 부드러운 깜빡임 펄스 연출) ──

        /// <summary>
        /// 하단 체인 연결 지점에 스냅 하이라이트 펄스를 켠다.
        /// </summary>
        public void ShowChainHighlight() => PlaySnapPulse(chainHighlightImage);

        /// <summary>
        /// 우측 값 연결 지점에 스냅 하이라이트 펄스를 켠다.
        /// </summary>
        public void ShowValueHighlight() => PlaySnapPulse(valueHighlightImage);

        /// <summary>
        /// 스냅 하이라이트 펄스를 멈추고 체인·값 하이라이트를 모두 끈다.
        /// </summary>
        public void ClearSnapHighlight()
        {
            StopSnapPulse();
            SetHighlight(chainHighlightImage, Color.clear);
            SetHighlight(valueHighlightImage, Color.clear);
        }

        /// <summary>
        /// 지정한 하이라이트 이미지에 무한 반복 알파 펄스를 재생한다.
        /// </summary>
        private void PlaySnapPulse(Image img)
        {
            // 테두리 모양과 보일 영역(아래 체인·오른쪽 값 칸 등)은 이미지의 BlockOutlineMesh와 머티리얼이 정한다.
            if (!img)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 스냅 하이라이트 이미지가 연결되지 않았습니다.");
                return;
            }
            if (_activeSnapImage == img && _snapHighlightTween != null && _snapHighlightTween.IsActive()) return;

            StopSnapPulse();

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
            if (!TryGetCompileHighlightTarget("에러", out Image target, out Color from)) return;

            target.color = from;
            int toggles = (Settings?.errorBlinkCount ?? Constants.HighlightSettings.ErrorBlinkCount) * 2;
            _compileHighlightTween = target
                .DOColor(Constants.HighlightColors.Error, Settings?.errorBlinkHalfDuration ?? Constants.HighlightSettings.ErrorBlinkHalfDuration)
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
            if (!TryGetCompileHighlightTarget("성공", out Image target, out Color from)) return;

            // 틴트는 본체 원래 색에 성공 색을 섞고, 외곽선은 성공 색 그대로 켠다
            Color success = Constants.HighlightColors.Success;
            Color to = Mode == HighlightMode.Tint ? Color.Lerp(from, success, Constants.HighlightSettings.TintStrength) : success;
            float duration = Settings?.successWaveFadeInDuration ?? Constants.HighlightSettings.SuccessWaveFadeInDuration;

            target.color = from;
            _compileHighlightTween = target.DOColor(to, duration).SetEase(Ease.OutSine).SetLink(gameObject);
        }

        /// <summary>
        /// HighlightMode에 맞는 컴파일 결과 표시 대상(본체 또는 외곽선)과 시작 색을 고르고 다른 쪽 표시는 지운다(대상이 없으면 경고 후 false).
        /// </summary>
        private bool TryGetCompileHighlightTarget(string purpose, out Image target, out Color from)
        {
            bool isTint = Mode == HighlightMode.Tint;
            if (isTint) SetHighlight(outlineImage, Color.clear);
            else ResetBodyTint();

            target = isTint ? bodyImage : outlineImage;
            if (!target)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 {(isTint ? "본체" : "외곽선")} 이미지가 연결되지 않아 {purpose} 표시를 건너뜁니다.");
                from = default;
                return false;
            }

            if (isTint) CacheBodyOriginalColor();
            from = isTint ? _bodyOriginalColor : Color.clear;
            return true;
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
        /// 하이라이트 이미지 색을 바꾼다 (테두리 두께만큼 넓히는 것은 BlockOutlineMesh가 메시로 한다).
        /// </summary>
        private static void SetHighlight(Image img, Color color)
        {
            if (img) img.color = color;
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
            // 블록 목록에 놓자마자 다른 탭이라 숨겨지면 OnEndDrag가 오지 않으므로 드래그 결과 행동 로그를 여기서 남긴다
            if (_isDragging) LogDragResult(null);

            StopSnapPulse();
            // 드롭하자마자 다른 탭으로 숨겨지면 OnEndDrag가 오지 않으므로, 대상 소켓의 스냅 하이라이트(무한 펄스)를 여기서 끈다
            ClearSnapTargets();
            if (!_cg) TryGetComponent(out _cg);
            if (_cg) _cg.blocksRaycasts = true;
            IsDragCancelled = false;
            _isDragging = false;
            _dragPointerId = NoDragPointer;
            _activeDrags.Remove(this);
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
            // 다른 손가락이 지금 이 블록을 끌고 있으면(두 손가락으로 같은 블록을 누름) 이 손가락의 드래그는 따르지 않는다.
            // 끌고 있지 않으면 새 손가락이 주인이 된다 — 입력 모듈은 포인터를 지울 때 OnEndDrag를 보내지 않아, 주인 기록만 보고 막으면 다시 집을 수 없게 된다
            if (_isDragging) return;
            _dragPointerId = e.pointerId;

            IsDragCancelled = false;
            _dragFromSocket = null;
            _dragOriginSocket = null;
            _dragFromInventory = false;

            // 코딩 패널에서 핀치 중이면 블록을 집지 않는다 — 패널 밖에 다른 손가락이 닿아 있는 것만으로는 막지 않는다
            if (CodingZoneZoom.IsPinching)
            {
                IsDragCancelled = true;
                return;
            }

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
            ClearSnapTargets();

            // 진행 중인 스냅 트윈을 즉시 완료 — 리페런트 후 잔여 틱이 캔버스 좌표계에 적용되어
            // 블록이 좌상단으로 날아가는 문제 방지 (홈 위치도 정착 좌표로 기록되도록 드래그 상태 저장 전에 수행)
            DOTween.Kill(_rt, true);

            _homeParent = transform.parent;
            _homeIndex = transform.GetSiblingIndex();
            _homeAnchoredPos = _rt.anchoredPosition;
            RememberDragOrigin();

            if (_homeParent.TryGetComponent(out BlockSocket homeSocket))
            {
                // 점유 기록이 이 블록일 때만 비운다 — 기록이 다른 블록이면 화면에 붙어 있는 그 블록의 자리를 지우게 된다
                if (homeSocket.Occupant == this)
                {
                    homeSocket.Release();

                    // 체인·안쪽 자리에서 떼면 아래에 붙어 있던 블록을 그 자리에 이어 붙인다
                    if (homeSocket is ChainOutSocket or InnerSocket) SpliceOutChild(homeSocket.Accept);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[CodingBlock] {name}이 붙어 있던 소켓의 점유 기록이 다른 블록이라 소켓을 비우지 않습니다.");
                }
            }

            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();
            _cg.blocksRaycasts = false;

            _isDragging = true;
            _activeDrags.Add(this);
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
            if (!IsDragPointer(e) || !_canvas || IsDragCancelled) return;

            _rt.anchoredPosition += e.delta / _canvas.scaleFactor;
            UpdateSnapHighlight();
        }

        /// <summary>
        /// 부모가 바뀌면 크기를 부모 기준 1로 맞춘다 — 확대/축소된 코딩 패널과 인벤토리를 오가도 블록이 놓인 곳의 배율을 따르게 한다.
        /// </summary>
        private void OnTransformParentChanged()
        {
            // 드래그 중(루트 캔버스 직속)에는 들어 올리기 전에 보이던 크기를 그대로 유지한다.
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
            bool isValue = SnapsHorizontally;
            BlockSocket socket = FindBestSnapSocket();

            // 내부 소켓은 소켓 자체를, 나머지는 소켓을 가진 블록의 연결부를 강조한다
            InnerSocket newInnerSocket = socket as InnerSocket;
            CodingBlock newTarget = socket && !newInnerSocket ? socket.Owner : null;
            _highlightedSocket = socket;

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
            // 같은 블록을 함께 누르고 있던 다른 손가락의 끝은 무시한다
            if (!IsDragPointer(e)) return;
            _dragPointerId = NoDragPointer;

            // 핀치로 취소된 드래그는 이미 제자리로 돌아갔으므로 손가락을 뗀 위치에 놓지 않는다
            if (IsDragCancelled)
            {
                IsDragCancelled = false;
                return;
            }

            _isDragging = false;
            _activeDrags.Remove(this);

            // 드래그 중 마지막으로 하이라이트한 소켓에 붙인다 — 놓는 순간 코딩 패널 배율로 크기가 바뀌어도 보이던 대로 붙고,
            // 하이라이트가 없었으면 붙지 않는다
            BlockSocket highlighted = _highlightedSocket;
            ClearSnapTargets();
            _cg.blocksRaycasts = true;

            if (CanAttachNow(highlighted) && AttachTo(highlighted))
            {
                LogDragResult(highlighted);
                if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.BlockAssembled);
                ClearDragCache();
                return;
            }

            if (!_canvas || transform.parent == _canvas.transform)
                ReturnHomeOrRelease(e);

            LogDragResult(null);
            ClearDragCache();
        }

        /// <summary>
        /// 행동 로그용으로 드래그를 시작한 자리(떼어 낸 소켓·블록 목록)를 기억한다 — 소켓을 비우기 전에 불러야 한다.
        /// </summary>
        private void RememberDragOrigin()
        {
            _dragOriginSocket = _homeParent.TryGetComponent(out BlockSocket socket) ? socket : null;
            _dragFromSocket = _dragOriginSocket ? DescribeSocket(_dragOriginSocket) : null;
            _dragFromInventory = _homeParent == InventoryParent;
        }

        /// <summary>
        /// 드래그 한 번의 결과를 체험자 행동 로그 한 줄로 남긴다 — 떼어 낸 자리와 놓은 자리(소켓·코딩 영역·블록 목록)를 함께 적는다.
        /// </summary>
        private void LogDragResult(BlockSocket attached)
        {
            // 코딩 영역 안에서 옮기기만 했거나 블록 목록에서 집었다가 그대로 돌려놓은 것처럼 연결이 바뀌지 않았으면 남기지 않는다.
            // 밀려난 블록이 꼬리로 옮겨지는 것처럼 체험자가 직접 끌지 않은 이동은 이 경로를 타지 않는다.
            if (_logger == null) return;

            // 떼었던 소켓에 그대로 다시 붙였으면 연결이 바뀌지 않았다
            if (attached && attached == _dragOriginSocket) return;

            string result = null;
            if (attached)
                result = ZString.Concat(DescribeSocket(attached), AttachVerb(attached));
            else if (_codingZone && transform.parent == _codingZone.transform)
                result = _dragFromSocket != null || _dragFromInventory ? "코딩 영역에 놓음" : null;
            else if (transform.parent == InventoryParent && !_dragFromInventory)
                result = "블록 목록으로 되돌림";

            if (result == null) return;

            string subject = VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider);
            if (_dragFromSocket != null)
                _logger.ZLogInformation($"[CodingBlock] {subject} '{name}' 블록을 {_dragFromSocket}에서 떼어 {result}.");
            else
                _logger.ZLogInformation($"[CodingBlock] {subject} '{name}' 블록을 {result}.");
        }

        /// <summary>
        /// 행동 로그에 쓸 소켓 자리를 주인 블록 이름과 소켓 종류로 적는다(예: "'시작하기' 아래", "'만약' 안", "'태양광 패널의 방향' 값 자리").
        /// </summary>
        private static string DescribeSocket(BlockSocket socket)
        {
            // 만약 블록의 머리 슬롯은 값이 아니라 조건을 받으므로 "조건 자리"로 적는다.
            CodingBlock owner = socket.Owner;
            bool isIfConditionSlot = owner && owner.Category == BlockCategory.FlowControl && !owner.IsRepeat;
            string place = socket switch
            {
                ChainOutSocket     => " 아래",
                InnerSocket        => " 안",
                ValueOutSocket     => isIfConditionSlot ? " 조건 자리" : " 값 자리",
                ConditionOutSocket => " 뒤",
                _                  => string.Empty
            };
            return ZString.Concat("'", owner ? owner.name : "알 수 없는 블록", "'", place);
        }

        /// <summary>
        /// 행동 로그에 쓸 소켓 종류별 붙이는 동작(체인은 붙임, 안쪽은 넣음, 값은 끼움, 조건은 이음).
        /// </summary>
        private static string AttachVerb(BlockSocket socket) => socket switch
        {
            InnerSocket        => "에 넣음",
            ValueOutSocket     => "에 끼움",
            ConditionOutSocket => "에 이음",
            _                  => "에 붙임"
        };

        /// <summary>
        /// 핀치가 시작되면 집어 든 블록을 모두 드래그 전 자리로 되돌린다 — 두 손가락 조작이 블록을 옮기지 않게 한다.
        /// </summary>
        public static void CancelActiveDrags()
        {
            if (_activeDrags.Count == 0) return;

            _cancelBuffer.Clear();
            _cancelBuffer.AddRange(_activeDrags);
            foreach (CodingBlock block in _cancelBuffer)
                if (block) block.CancelDrag();
            _cancelBuffer.Clear();
        }

        /// <summary>
        /// 드래그를 취소하고 드래그 전 자리로 되돌린다.
        /// </summary>
        private void CancelDrag()
        {
            // 이후 이 포인터의 OnDrag/OnDrop/OnEndDrag는 무시된다.
            _activeDrags.Remove(this);
            if (!_isDragging) return;

            _isDragging = false;
            IsDragCancelled = true;

            ClearSnapTargets();
            _cg.blocksRaycasts = true;

            RestoreDragHome();
            ClearDragCache();
        }

        /// <summary>
        /// 드래그 시작 전 자리로 되돌린다.
        /// </summary>
        private void RestoreDragHome()
        {
            // 소켓이었으면 다시 받게(Accept) 해서, 떼어낼 때 위로 이어 붙였던 아래 블록까지 원래 순서로 복원한다.
            Transform home = _homeParent;
            if (!home)
            {
                ReturnToInventory();
                return;
            }

            BlockFactory.AttachSockets(this);

            if (home.TryGetComponent(out BlockSocket homeSocket))
            {
                // 끄는 사이 다른 손가락이 그 자리를 채웠거나 주인 블록을 블록 목록으로 옮겼으면 덮어쓰지 않고 블록 목록으로 돌린다
                if (CanAttachNow(homeSocket))
                {
                    homeSocket.Accept(this);
                }
                else
                {
                    if (_logger != null) _logger.ZLogInformation($"[CodingBlock] {name}의 원래 자리를 다른 블록이 쓰고 있어 블록 목록으로 되돌립니다.");
                    ReturnToInventory();
                }
            }
            else if (_codingZone && home == _codingZone.transform)
            {
                transform.SetParent(home, false);
                transform.SetSiblingIndex(_homeIndex);
                _rt.anchoredPosition = _homeAnchoredPos;
            }
            else
            {
                ReturnToInventory();
                if (transform.parent == home) transform.SetSiblingIndex(_homeIndex);
            }
        }

        /// <summary>
        /// 드래그 중 켜 둔 스냅 대상 소켓의 하이라이트를 끄고 대상을 비운다.
        /// </summary>
        private void ClearSnapTargets()
        {
            if (_snapTarget) _snapTarget.ClearSnapHighlight();
            _snapTarget = null;
            if (_snapInnerSocket) _snapInnerSocket.ClearSnapHighlight();
            _snapInnerSocket = null;
            _highlightedSocket = null;
        }

        /// <summary>
        /// 지금 위치에서 하이라이트할 소켓을 고른다 — 손을 떼면 마지막으로 고른 이 소켓에 붙는다.
        /// </summary>
        private BlockSocket FindBestSnapSocket()
        {
            // 가로 연결 블록은 조건 연결(ConditionOut)을 먼저, 없으면 값 슬롯(ValueOut)을, 세로 연결 블록은 체인·내부 소켓 중 더 가까운 쪽을 고른다.
            if (SnapsHorizontally)
            {
                if (PrefersConditionSocket)
                {
                    ConditionOutSocket conditionSocket = FindSnapConditionOutSocket();
                    if (conditionSocket) return conditionSocket;
                }
                return FindSnapValueOutSocket();
            }

            ChainOutSocket chainSocket = FindSnapOutSocket(out float chainSqr);
            InnerSocket innerSocket = FindSnapInnerSocket(out float innerSqr);

            // 두 후보가 모두 범위 안이면 더 가까운 쪽 우선
            if (chainSocket && (!innerSocket || chainSqr <= innerSqr)) return chainSocket;
            return innerSocket;
        }

        /// <summary>
        /// 드래그 중 하이라이트한 소켓이 손을 뗀 지금도 이 블록을 받을 수 있는지 다시 확인한다.
        /// </summary>
        public bool CanAttachNow(BlockSocket socket)
        {
            if (!socket || !socket.isActiveAndEnabled) return false;

            // 다른 손가락이 같은 소켓에 먼저 붙였거나 소켓 주인 블록을 목록으로 옮겼을 수 있다 —
            // 값·조건 소켓은 찬 자리에 받으면 먼저 붙은 블록을 덮어써 화면과 점유 기록이 어긋난다
            CodingBlock owner = socket.Owner;
            if (!owner || !_codingZone || !_codingZone.Contains(owner)) return false;

            return socket switch
            {
                ChainOutSocket chainOut => chainOut.CanAccept(this),
                InnerSocket inner       => inner.CanAccept(this),
                _                       => socket.IsEmpty
            };
        }

        /// <summary>
        /// 소켓 부착 공통 절차 — 이 블록의 소켓을 갖춘 뒤 대상 소켓에 인계한다.
        /// </summary>
        private bool AttachTo(BlockSocket socket)
        {
            BlockFactory.AttachSockets(this);
            socket.Accept(this);
            return true;
        }

        // GetWorldCorners 인덱스 — 0:좌하 1:좌상 2:우상 3:우하
        private const int CornerBottomLeft = 0;
        private const int CornerTopLeft    = 1;

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
        /// ChainInSocket 위치를 체인 스냅 기준점으로 반환한다 — 진입 소켓이 없는 블록(시작하기·함수 정의)은 다른 블록 아래나 안에 붙지 않는다.
        /// </summary>
        private bool TryGetChainSnapOrigin(out Vector2 pos)
        {
            if (_chainInSocket)
            {
                pos = (Vector2)_chainInSocket.transform.position;
                return true;
            }

            pos = default;
            return false;
        }

        /// <summary>
        /// 가로 연결(값/조건) 기준점으로 해당 In 소켓 위치를, 없으면 블록 좌측 중앙을 반환한다.
        /// </summary>
        private bool TryGetHorizontalSnapOrigin(Component inSocket, out Vector2 pos)
        {
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
        /// 이 블록의 체인 진입 소켓 아래쪽 반경 안에서 가장 가까운 ChainOutSocket을 찾는다.
        /// </summary>
        private ChainOutSocket FindSnapOutSocket(out float bestSqr)
        {
            EnsureDragCache();
            if (!TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            ChainOutSocket best = null;
            float minSqr = ChainSnapRadius * ChainSnapRadius;

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
            EnsureDragCache();
            if (Category == BlockCategory.Control || !TryGetChainSnapOrigin(out Vector2 myPos))
            {
                bestSqr = float.MaxValue;
                return null;
            }

            InnerSocket best = null;
            float minSqr = ChainSnapRadius * ChainSnapRadius;

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
            // Logic(그리고)은 조건 블록의 ConditionOut에만 연결 — 만약 헤더 슬롯 직접 스냅 금지
            if (Category == BlockCategory.Logic) return null;

            EnsureDragCache();
            if (!TryGetHorizontalSnapOrigin(_valueInSocket, out Vector2 myPos)) return null;

            ValueOutSocket best = null;
            float minSqr = SnapRadius * SnapRadius;

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
                    if (Category == BlockCategory.Condition && targetBlock.Category != BlockCategory.FlowControl) continue;

                    // 반복하기의 헤더 슬롯은 조건용이 아니므로 조건 블록은 스냅 제외 (만약 전용) —
                    // 지금 반복하기 프리팹에는 값 슬롯이 없지만, 생기더라도 조건을 받지 않게 막아 둔다
                    if (Category == BlockCategory.Condition
                        && targetBlock.IsRepeat)
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
            EnsureDragCache();
            if (!TryGetHorizontalSnapOrigin(_conditionInSocket, out Vector2 myPos)) return null;

            ConditionOutSocket best = null;
            float minSqr = SnapRadius * SnapRadius;

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
        /// 안전망 — 소켓 없는 블록(완성하기 등)이 들어와 밀려났는데 넘길 소켓이 없을 때 코딩 패널 직속으로 옮긴다.
        /// </summary>
        public void MoveToCodingZone()
        {
            if (!_codingZone)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingBlock] {name}에 CodingZone이 연결되지 않아 코딩 패널로 옮기지 못했습니다.");
                return;
            }

            transform.SetParent(_codingZone.transform, true);
            SetHome(_codingZone.transform);
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

                // ReturnToInventory가 자식 블록의 하위 소켓까지 함께 해제한다
                socket.Release();
                child.ReturnToInventory();
            }
        }

        /// <summary>
        /// 시작하기/완성하기 블록을 코딩 패널의 고정 자리(좌상단 / 좌하단)에 배치한다.
        /// </summary>
        public static void ApplyControlBlockLayout(RectTransform rt, bool isStart, CodingZone codingZone)
        {
            // 최초 스폰(BlockSpawner)과 인벤토리 반입 시 복귀(ReturnToInventory)가 같은 규칙을 쓰도록 공유한다.
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
