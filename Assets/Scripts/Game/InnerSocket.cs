using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Game
{
    // FlowControl 블록 내부 영역의 진입 소켓.
    // ChainOutSocket과 동일한 Accept/Release 인터페이스를 가지며,
    // CodingBlock의 스냅 탐색이 방향 제한 없이 이 소켓을 찾아 스냅한다.
    public class InnerSocket : BlockSocket
    {
        // 프리팹에서는 직렬화로 연결, 코드 생성 경로에서는 SetEmptyIndicator로 주입
        [SerializeField] private GameObject _emptyIndicator;

        private Image _highlightImg;
        private Tweener _pulseTween;
        private ILogger<InnerSocket> _logger;

        /// <summary>
        /// 로거를 주입받는다 (블록 생성 직후 BlockSpawner가 블록 계층 전체에 주입).
        /// </summary>
        [Inject]
        public void Construct(ILogger<InnerSocket> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 코드로 만든 Inner 컨테이너의 빈 상태 표시 오브젝트를 연결한다.
        /// </summary>
        public void SetEmptyIndicator(GameObject go) => _emptyIndicator = go;

        // ── 내부 슬롯 스냅 하이라이트 (헤더 하단 내부 소켓 노치 라인을 따라 초록색 펄스) ──

        /// <summary>
        /// 내부 진입 지점에 초록색 스냅 하이라이트 펄스를 켠다.
        /// </summary>
        public void ShowSnapHighlight()
        {
            Image img = GetOrAddHighlightImage();
            if (!img) return;

            if (_pulseTween != null && _pulseTween.IsActive()) return;

            StopSnapPulse();

            Color baseColor = Constants.HighlightColors.Snap;
            baseColor.a = Constants.HighlightSettings.SnapPulseMaxAlpha;
            img.color = baseColor;

            _pulseTween = img.DOFade(Constants.HighlightSettings.SnapPulseMinAlpha, Constants.HighlightSettings.SnapPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 내부 진입 스냅 하이라이트를 끈다.
        /// </summary>
        public void ClearSnapHighlight()
        {
            StopSnapPulse();
        }

        /// <summary>
        /// 펄스 트윈을 멈추고 하이라이트를 투명하게 되돌린다.
        /// </summary>
        private void StopSnapPulse()
        {
            if (_pulseTween != null && _pulseTween.IsActive())
            {
                _pulseTween.Kill();
                _pulseTween = null;
            }

            if (_highlightImg)
            {
                _highlightImg.color = Color.clear;
            }
        }

        /// <summary>
        /// 소유 블록의 외곽선 옆에 내부 진입 하이라이트 이미지를 (없으면 만들어) 반환한다.
        /// 블록 외곽선과 같은 크기·스프라이트 설정으로 만들고, 머티리얼이 ㄷ자 안쪽 진입부(머리 아래 가장자리·돌기)만 남긴다.
        /// </summary>
        private Image GetOrAddHighlightImage()
        {
            if (_highlightImg) return _highlightImg;

            CodingBlock parentBlock = Owner;
            Image outline = parentBlock ? parentBlock.OutlineImage : null;
            if (!outline)
            {
                if (_logger != null) _logger.ZLogWarning($"[InnerSocket] {name}의 소유 블록 외곽선을 찾지 못해 내부 진입 하이라이트를 표시할 수 없습니다.");
                return null;
            }

            Transform container = outline.transform.parent;

            // 이 소켓이 직접 만들어 상수 이름을 붙인 자식이라 이름 탐색이 허용된다
            Image existingImg = FindChildComponent<Image>(container, Constants.BlockParts.InnerSnapHighlight);
            if (existingImg)
            {
                _highlightImg = existingImg;
                return existingImg;
            }

            GameObject go = new GameObject(Constants.BlockParts.InnerSnapHighlight, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(container, false);

            // 다른 하이라이트와 함께 본체 뒤에 깔리도록 ValueHighlight(없으면 외곽선) 바로 뒤에 둔다
            Image valueHighlight = parentBlock.ValueHighlightImage;
            Transform after = valueHighlight && valueHighlight.transform.parent == container ? valueHighlight.transform : outline.transform;
            go.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);

            go.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform outlineRt = outline.rectTransform;
            go.TryGetComponent(out RectTransform rt);
            rt.anchorMin = outlineRt.anchorMin;
            rt.anchorMax = outlineRt.anchorMax;
            rt.offsetMin = outlineRt.offsetMin;
            rt.offsetMax = outlineRt.offsetMax;

            go.TryGetComponent(out Image img);
            img.sprite = outline.sprite;
            img.type = outline.type;
            img.fillCenter = outline.fillCenter;
            img.pixelsPerUnitMultiplier = outline.pixelsPerUnitMultiplier;
            img.preserveAspect = false;
            img.material = BlockFactory.GetInnerOutlineMaterial(outline.sprite);
            img.color = Color.clear;
            img.raycastTarget = false;
            go.AddComponent<BlockOutlineMesh>();

            _highlightImg = img;
            return img;
        }

        /// <summary>
        /// 들어오는 블록 체인에 제어 블록이 없고 cascade가 완료될 수 있을 때만 받는다.
        /// </summary>
        public bool CanAccept(CodingBlock incoming)
        {
            if (incoming && HasControlBlockInChain(incoming))
                return false;
            return CanFit(incoming, Occupant);
        }

        /// <summary>
        /// 블록을 내부 첫 자리로 받아 스냅시키고, 원래 있던 블록은 들어온 체인의 꼬리에 이어 붙인다.
        /// </summary>
        public override void Accept(CodingBlock block)
        {
            ClearSnapHighlight();

            CodingBlock displaced = Occupant;
            SetOccupant(block);
            block.SnapInto(transform, ChainOutSocket.ComputeChainSnapOffset(block)).Forget();
            if (_emptyIndicator) _emptyIndicator.SetActive(false);

            AttachDisplacedToTail(block, displaced);
        }

        /// <summary>
        /// 점유를 비우고 하이라이트를 끈 뒤 빈 상태 표시를 다시 켠다.
        /// </summary>
        public override void Release()
        {
            ClearSnapHighlight();
            base.Release();
            if (_emptyIndicator) _emptyIndicator.SetActive(true);
        }

        /// <summary>
        /// 비활성화될 때 하이라이트를 끈다.
        /// </summary>
        private void OnDisable()
        {
            ClearSnapHighlight();
        }

        /// <summary>
        /// 파괴될 때 하이라이트 트윈을 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            ClearSnapHighlight();
        }

#if UNITY_EDITOR
        // 기즈모로 그리는 기준 반경 — 실제 스냅 반경은 3_Game.json(snapRadius·chainSnapRadius) × 코딩 패널 배율이라 다를 수 있다
        private const float SnapRadius = 120f;

        /// <summary>
        /// 씬 뷰에 소켓 위치와 스냅 범위를 표시한다.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            Color markerColor = IsEmpty ? new Color(1f, 0.6f, 0f, 0.9f)  : new Color(1f, 0.3f, 0.3f, 0.9f);
            Color rangeColor  = IsEmpty ? new Color(1f, 0.6f, 0f, 0.08f) : new Color(1f, 0.3f, 0.3f, 0.08f);

            SocketGizmos.DrawCross(rt, markerColor, arm: 12f, dotRadius: 5f);
            SocketGizmos.DrawSnapRange(rt, Vector3.left, SnapRadius, markerColor, rangeColor);
            SocketGizmos.DrawLabel(rt, $"InnerSocket  r={SnapRadius}", new Color(1f, 0.6f, 0f), yOffset: 18f);
        }
#endif
    }
}
