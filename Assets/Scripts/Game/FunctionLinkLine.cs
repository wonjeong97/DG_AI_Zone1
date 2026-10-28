using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Game
{
    // 코딩 영역(Content)의 첫 자식으로 두어 블록 뒤에 그려지고 코딩 영역과 함께 스크롤·확대된다 — 블록 사이 빈 곳에만 점선이 보인다.
    // 레벨 레이아웃에 함수 실행하기·함수 만들기 블록이 하나씩만 있어 처음 찾은 것끼리 잇는다.
    // 점이 흐르는 움직임은 머티리얼의 텍스처 오프셋만 바꿔, 블록이 움직이지 않는 동안에는 캔버스를 다시 그리지 않는다.
    /// <summary>
    /// 코딩 영역에 함수 실행하기·함수 만들기 블록이 함께 놓이면 실행하기에서 만들기 쪽으로 흐르는 점선으로 둘을 잇는다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class FunctionLinkLine : MonoBehaviour
    {
        [SerializeField] private CodingZone codingZone;
        [Tooltip("점 하나와 다음 점까지의 간격(코딩 영역 배율 1 기준 px) — 굵기의 2배면 점이 동그랗다")]
        [Min(1f)] [SerializeField] private float dotSpacing = 28f;
        [Tooltip("점선 굵기(코딩 영역 배율 1 기준 px)")]
        [SerializeField] private float thickness = 14f;
        [Tooltip("점이 함수 실행하기에서 함수 만들기 쪽으로 흐르는 속도(px/초)")]
        [SerializeField] private float flowSpeed = 60f;

        // 점 텍스처 한 칸 — 왼쪽 반에 점 하나, 오른쪽 반은 빈칸(가로로 반복해 점선이 된다)
        private const int DotTextureWidth = 32;
        private const int DotTextureHeight = 16;

        private ILogger<FunctionLinkLine> _logger;
        private RawImage _image;
        private RectTransform _rt;
        private RectTransform _zoneRt;
        private Texture2D _dotTexture;
        private Material _material;
        private CodingBlock _callBlock;
        private CodingBlock _defBlock;
        private Vector2 _lastStart;
        private Vector2 _lastEnd;
        private float _flowOffset;

        /// <summary>
        /// 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<FunctionLinkLine> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 점 텍스처와 흐름용 머티리얼을 만들고 선을 숨긴 채 시작한다.
        /// </summary>
        private void Awake()
        {
            TryGetComponent(out _image);
            _rt = (RectTransform)transform;
            if (codingZone) _zoneRt = (RectTransform)codingZone.transform;

            // 시작점(함수 실행하기)을 기준으로 끝점 쪽으로 늘이고 돌린다
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0f, 0.5f);

            _dotTexture = CreateDotTexture();
            _material = new Material(Graphic.defaultGraphicMaterial) { name = "FunctionLinkLine (Instance)" };
            _image.texture = _dotTexture;
            _image.material = _material;
            _image.color = Constants.CategoryColors.Function;
            _image.raycastTarget = false;
            _image.enabled = false;
        }

        /// <summary>
        /// 코딩 영역 연결이 빠졌으면 경고한다 — 주입은 Awake 뒤에 끝난다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[FunctionLinkLine] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");
            else if (!codingZone)
                _logger.ZLogWarning($"[FunctionLinkLine] codingZone이 연결되지 않아 함수 블록 연결 점선을 그리지 않습니다.");
        }

        /// <summary>
        /// 코드로 만든 텍스처와 머티리얼을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_dotTexture) Destroy(_dotTexture);
            if (_material) Destroy(_material);
        }

        /// <summary>
        /// 두 블록이 모두 코딩 영역에 있으면 선을 두 블록 이름 사이에 맞추고 점을 흘리며, 아니면 숨긴다.
        /// </summary>
        private void LateUpdate()
        {
            if (!_zoneRt || !TryGetLinkPoints(out Vector2 start, out Vector2 end))
            {
                if (_image.enabled) _image.enabled = false;
                return;
            }

            // 블록이 움직였을 때만 위치·길이를 고쳐 캔버스를 다시 그리게 한다
            if (!_image.enabled || start != _lastStart || end != _lastEnd)
            {
                ApplyGeometry(start, end);
                _lastStart = start;
                _lastEnd = end;
                _image.enabled = true;
            }

            // 오프셋을 줄이면 무늬가 선 방향(함수 만들기 쪽)으로 흐른다
            _flowOffset = Mathf.Repeat(_flowOffset + flowSpeed * Time.deltaTime / dotSpacing, 1f);
            _material.mainTextureOffset = new Vector2(-_flowOffset, 0f);
        }

        /// <summary>
        /// 함수 실행하기·함수 만들기 블록이 둘 다 코딩 영역에 놓여 있으면 두 블록 이름의 가운데를 코딩 영역 좌표로 돌려준다.
        /// </summary>
        private bool TryGetLinkPoints(out Vector2 start, out Vector2 end)
        {
            if (!_callBlock || !_defBlock) FindFunctionBlocks();

            end = default;
            return TryGetAnchor(_callBlock, out start) && TryGetAnchor(_defBlock, out end);
        }

        /// <summary>
        /// 생성된 블록 목록에서 처음 나오는 함수 실행하기·함수 만들기 블록을 기억한다 — 함수 블록이 없는 레벨은 찾지 못한 채로 둔다.
        /// </summary>
        private void FindFunctionBlocks()
        {
            IReadOnlyList<CodingBlock> blocks = codingZone.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                CodingBlock block = blocks[i];
                if (!block) continue;

                if (!_callBlock && block.Category == BlockCategory.Function) _callBlock = block;
                else if (!_defBlock && block.Category == BlockCategory.FunctionDef) _defBlock = block;
            }
        }

        /// <summary>
        /// 블록이 코딩 영역 안에 보이면 블록 이름(없으면 블록)의 가운데를 코딩 영역 좌표로 돌려준다 — 블록 목록에 있거나 끄는 중이면 false.
        /// </summary>
        private bool TryGetAnchor(CodingBlock block, out Vector2 local)
        {
            local = default;
            if (!block || !block.isActiveAndEnabled || !codingZone.Contains(block)) return false;

            RectTransform target = block.LabelText ? block.LabelText.rectTransform : (RectTransform)block.transform;
            local = _zoneRt.InverseTransformPoint(target.TransformPoint(target.rect.center));
            return true;
        }

        /// <summary>
        /// 선을 시작점에 두고 끝점 쪽으로 돌려 늘이며, 길이만큼 점 무늬를 반복한다.
        /// </summary>
        private void ApplyGeometry(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;

            _rt.localPosition = start;
            _rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _rt.sizeDelta = new Vector2(length, thickness);
            _image.uvRect = new Rect(0f, 0f, length / dotSpacing, 1f);
        }

        /// <summary>
        /// 가로로 반복하면 점선이 되는 흰 점 텍스처를 만든다 — 색은 RawImage 색으로 입힌다.
        /// </summary>
        private static Texture2D CreateDotTexture()
        {
            Texture2D texture = new Texture2D(DotTextureWidth, DotTextureHeight, TextureFormat.RGBA32, false)
            {
                name = "FunctionLinkDots",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // 점 가장자리는 1px 안에서 투명해져 계단이 지지 않는다
            const float radius = DotTextureHeight * 0.5f - 1f;
            Vector2 center = new Vector2(DotTextureHeight * 0.5f, DotTextureHeight * 0.5f);
            Color32[] pixels = new Color32[DotTextureWidth * DotTextureHeight];
            for (int y = 0; y < DotTextureHeight; y++)
            {
                for (int x = 0; x < DotTextureWidth; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    byte alpha = (byte)(Mathf.Clamp01(radius + 0.5f - distance) * byte.MaxValue);
                    pixels[y * DotTextureWidth + x] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
