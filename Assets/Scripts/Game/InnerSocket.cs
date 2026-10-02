using System.Collections.Generic;
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
        /// 소유 블록의 헤더 컨테이너에 내부 진입 하이라이트 이미지를 (없으면 만들어) 반환한다.
        /// </summary>
        private Image GetOrAddHighlightImage()
        {
            if (_highlightImg) return _highlightImg;

            CodingBlock parentBlock = Owner;
            Transform container = null;
            Sprite blockSprite = null;

            if (parentBlock)
            {
                // Header 컨테이너 탐색 (root의 첫 번째 자식, e.g. Label / Header_...)
                if (parentBlock.transform.childCount > 0)
                {
                    Transform firstChild = parentBlock.transform.GetChild(0);
                    if (firstChild != transform.parent)
                        container = firstChild;
                }

                // 하이라이트 모양은 블록 외곽선과 같은 스프라이트를 쓴다
                if (parentBlock.OutlineImage) blockSprite = parentBlock.OutlineImage.sprite;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[InnerSocket] {name}의 소유 블록이 등록되지 않아 하이라이트 모양을 알 수 없습니다.");
            }

            if (!container)
                container = transform.parent ? transform.parent : transform;

            // 이 소켓이 직접 만들어 상수 이름을 붙인 자식이라 이름 탐색이 허용된다
            Image existingImg = FindChildComponent<Image>(container, Constants.BlockParts.InnerSnapHighlight);
            if (existingImg)
            {
                _highlightImg = existingImg;
                return existingImg;
            }

            GameObject go = new GameObject(Constants.BlockParts.InnerSnapHighlight, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(container, false);

            // ValueHighlight 바로 아래에 배치하여 렌더링 순서 최적화
            Image valueHighlight = parentBlock ? parentBlock.ValueHighlightImage : null;
            if (valueHighlight && valueHighlight.transform.parent == container)
                go.transform.SetSiblingIndex(valueHighlight.transform.GetSiblingIndex() + 1);
            else
                go.transform.SetAsFirstSibling();

            go.AddComponent<LayoutElement>().ignoreLayout = true;

            go.TryGetComponent(out RectTransform rt);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, Constants.HighlightSettings.InnerHighlightOffsetBottom);
            rt.offsetMax = new Vector2(0f, -Constants.HighlightSettings.InnerHighlightOffsetTop);

            go.TryGetComponent(out Image img);
            img.sprite = blockSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.material = BlockFactory.SpriteFillMaterialInner;
            img.color = Color.clear;
            img.raycastTarget = false;

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
        /// 들어오는 블록의 체인·내부 소켓 어딘가에 Control 블록(시작하기/완성하기)이 섞여 있는지 확인한다.
        /// 제어 블록은 FlowControl 내부로 들어갈 수 없다.
        /// 드래그 중 매 프레임 호출되므로 리스트를 만들지 않고 블록의 소켓 목록을 인덱스로 순회한다(재귀라 공용 버퍼도 쓸 수 없음).
        /// </summary>
        private static bool HasControlBlockInChain(CodingBlock block)
        {
            CodingBlock current = block;
            while (current)
            {
                if (current.Category == BlockCategory.Control) return true;

                IReadOnlyList<BlockSocket> sockets = current.Sockets;
                for (int i = 0; i < sockets.Count; i++)
                {
                    if (sockets[i] is InnerSocket innerSocket && innerSocket
                        && innerSocket.Occupant && HasControlBlockInChain(innerSocket.Occupant))
                        return true;
                }

                ChainOutSocket chainOut = ChainOutSocket.OfBlock(current);
                current = chainOut ? chainOut.Occupant : null;
            }
            return false;
        }

        /// <summary>
        /// 블록을 내부 첫 자리로 받아 스냅시키고, 원래 있던 블록은 새 블록 아래로 밀어 붙인다.
        /// </summary>
        public void Accept(CodingBlock block)
        {
            ClearSnapHighlight();

            CodingBlock displaced = Occupant;
            SetOccupant(block);
            block.SnapInto(transform, ChainOutSocket.ComputeChainSnapOffset(block)).Forget();
            if (_emptyIndicator) _emptyIndicator.SetActive(false);

            if (!displaced) return;

            // 직속 소켓만 사용 — 컨테이너 내부 하위 소켓을 잡아 치환 블록이 잘못 들어가는 문제 방지
            ChainOutSocket nextOut = ChainOutSocket.OfBlock(block);
            if (nextOut)
                nextOut.Accept(displaced);
            else
                displaced.MoveToCodingZone();
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
