using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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

        public void SetEmptyIndicator(GameObject go) => _emptyIndicator = go;

        // ── 내부 슬롯 스냅 하이라이트 (헤더 하단 내부 소켓 노치 라인을 따라 초록색 펄스) ──
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

        public void ClearSnapHighlight()
        {
            StopSnapPulse();
        }

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

        private Image GetOrAddHighlightImage()
        {
            if (_highlightImg) return _highlightImg;

            Transform innerTransform = transform.parent ? transform.parent : transform;
            Transform existing = innerTransform.Find("InnerSnapHighlight");
            if (existing && existing.TryGetComponent<Image>(out Image existingImg))
            {
                _highlightImg = existingImg;
                return existingImg;
            }

            // 부모 CodingBlock에서 스프라이트 참조 가져오기
            CodingBlock parentBlock = GetComponentInParent<CodingBlock>();
            Sprite blockSprite = null;
            if (parentBlock)
            {
                Image bg = parentBlock.GetComponentInChildren<Image>(true);
                if (bg) blockSprite = bg.sprite;
            }

            GameObject go = new GameObject("InnerSnapHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(innerTransform, false);
            go.transform.SetAsFirstSibling();
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            rt.sizeDelta = new Vector2(0f, 60f);

            Image img = go.GetComponent<Image>();
            img.sprite = blockSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.material = BlockFactory.SpriteFillMaterialBottom;
            img.color = Color.clear;
            img.raycastTarget = false;

            _highlightImg = img;
            return img;
        }

        public bool CanAccept(CodingBlock incoming)
        {
            if (incoming != null && HasControlBlockInChain(incoming))
                return false;
            return CanFit(incoming, Occupant);
        }

        // 들어오는 블록의 체인·내부 소켓 어딘가에 Control 블록(시작하기/완성하기)이 섞여 있는지 확인.
        // 제어 블록은 FlowControl 내부로 들어갈 수 없다.
        private static bool HasControlBlockInChain(CodingBlock block)
        {
            CodingBlock current = block;
            while (current)
            {
                if (current.Category == BlockCategory.Control) return true;

                foreach (InnerSocket innerSocket in current.GetComponentsInChildren<InnerSocket>(true))
                {
                    if (innerSocket.Occupant && HasControlBlockInChain(innerSocket.Occupant))
                        return true;
                }

                ChainOutSocket chainOut = current.GetComponentInChildren<ChainOutSocket>();
                current = chainOut ? chainOut.Occupant : null;
            }
            return false;
        }

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
                MoveToCodingZone(displaced);
        }

        public override void Release()
        {
            ClearSnapHighlight();
            base.Release();
            if (_emptyIndicator) _emptyIndicator.SetActive(true);
        }

        private void OnDisable()
        {
            ClearSnapHighlight();
        }

        private void OnDestroy()
        {
            ClearSnapHighlight();
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

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
