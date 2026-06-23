using DG.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Game
{
    public static class BlockFactory
    {
        private const string BlockImagePath = "Images/Blocks/";

        // 카테고리 + label(Start/End 구분)로 스프라이트 로드
        private static Sprite LoadSprite(BlockCategory cat, string label = "")
        {
            string name = cat switch
            {
                BlockCategory.Control => label == "시작하기" ? "Start" : "End",
                BlockCategory.Command => "Command",
                BlockCategory.Value => "Value",
                BlockCategory.FlowControl => "FlowControl",
                BlockCategory.ConditionAction => "ConditionAction",
                BlockCategory.Action => "Action",
                BlockCategory.Logic => "Logic",
                _ => null
            };
            return name != null ? Resources.Load<Sprite>(BlockImagePath + name) : null;
        }

        // 색상은 스프라이트 없을 때 폴백용으로만 사용
        public static Color GetColor(BlockCategory cat) => cat switch
        {
            BlockCategory.Control => new Color(0.20f, 0.20f, 0.20f),
            BlockCategory.Command => new Color(0.82f, 0.36f, 0.28f),
            BlockCategory.Value => new Color(0.27f, 0.60f, 0.85f),
            BlockCategory.FlowControl => new Color(0.92f, 0.57f, 0.08f),
            BlockCategory.ConditionAction => new Color(0.86f, 0.40f, 0.63f),
            BlockCategory.Action => new Color(0.28f, 0.72f, 0.66f),
            BlockCategory.Logic => new Color(0.33f, 0.73f, 0.36f),
            _ => Color.white
        };

        // ── 진입점 ──────────────────────────────────────────────
        public static GameObject Create(BlockEntry entry, Canvas rootCanvas, bool draggable = true)
        {
            return entry.category switch
            {
                BlockCategory.Command     => CreateCommandBlock(entry, rootCanvas, draggable),
                BlockCategory.FlowControl => CreateFlowBlock(entry, rootCanvas, draggable),
                _                         => CreateSimpleBlock(entry, rootCanvas, draggable)
            };
        }

        // ── 단순 블록 ───────────────────────────────────────────
        private static GameObject CreateSimpleBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            var sprite = LoadSprite(entry.category, entry.label);
            var go = NewRect(entry.label, 220f, 56f);
            AddImage(go, GetColor(entry.category), sprite, isBlockBody: true);
            go.AddComponent<CanvasGroup>();
            if (draggable)
            {
                AddDraggable(go, entry.category, rootCanvas);
            }

            AddLabel(go, entry.label);

            // Logic 블록은 수평 체인 슬롯 포함
            if (entry.category == BlockCategory.Logic && entry.chainBlocks != null)
                AppendChain(go, entry.chainBlocks, rootCanvas, draggable);

            return go;
        }

        // ── Command 블록 ────────────────────────────────────────
        private static GameObject CreateCommandBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            var cmdSprite = LoadSprite(BlockCategory.Command);
            float cmdW = cmdSprite?.rect.width  ?? 260f;
            float cmdH = cmdSprite?.rect.height ?? 56f;

            var go = NewRect(entry.label, cmdW, cmdH);
            go.AddComponent<CanvasGroup>();
            if (draggable)
                AddDraggable(go, entry.category, rootCanvas);

            var labelPart = NewRect("Label", cmdW, cmdH);
            labelPart.transform.SetParent(go.transform, false);
            labelPart.TryGetComponent<RectTransform>(out var labelRT);
            labelRT.anchorMin = labelRT.anchorMax = labelRT.pivot = new Vector2(0f, 1f);
            labelRT.anchoredPosition = Vector2.zero;
            AddImage(labelPart, GetColor(entry.category), cmdSprite, isBlockBody: true);
            AddLabel(labelPart, entry.label, 24);

            return go;
        }

        // ── FlowControl 블록 (C자형) ────────────────────────────
        private static GameObject CreateFlowBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            var color = GetColor(entry.category);

            var go = NewRect(entry.label, 0f, 0f);
            go.AddComponent<CanvasGroup>();

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (draggable)
            {
                AddDraggable(go, entry.category, rootCanvas);
            }

            // 헤더
            go.transform.SetParent(go.transform, false); // placeholder, set parent externally
            AppendFlowHeader(go.transform, entry.label, color, 56f);

            // 내부 컨테이너
            AppendInnerContainer(go.transform, entry.innerBlocks, color, rootCanvas, draggable);

            // else 분기 (만약 블록)
            if (entry.elseBlocks != null && entry.elseBlocks.Length > 0)
            {
                AppendFlowHeader(go.transform, "아니면", color, 44f);
                AppendInnerContainer(go.transform, entry.elseBlocks, color, rootCanvas, draggable);
            }

            // 푸터 (닫는 괄호)
            AppendFlowFooter(go.transform, color);

            return go;
        }

        // ── FlowControl 헬퍼 ────────────────────────────────────
        private static void AppendFlowHeader(Transform parent, string label, Color color, float height)
        {
            var sprite = LoadSprite(BlockCategory.FlowControl);
            float spriteH = sprite != null ? sprite.rect.height : height;
            var h = NewRect("Header_" + label, 0f, spriteH);
            h.transform.SetParent(parent, false);
            AddImage(h, color, sprite, isBlockBody: true);
            AddLabel(h, label, 26);

            var le = h.AddComponent<LayoutElement>();
            le.preferredHeight = spriteH;
            le.flexibleWidth = 1f;
        }

        private static void AppendInnerContainer(Transform parent, BlockEntry[] blocks, Color color,
            Canvas rootCanvas, bool draggable)
        {
            var inner = NewRect("Inner", 0f, 0f);
            inner.transform.SetParent(parent, false);
            AddImage(inner, new Color(color.r, color.g, color.b, 0.25f));

            var vlg = inner.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 8, 4, 4);
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            var csf = inner.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var le = inner.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            if (blocks != null && blocks.Length > 0)
            {
                foreach (var b in blocks)
                {
                    var child = Create(b, rootCanvas, draggable);
                    child.transform.SetParent(inner.transform, false);
                }
            }
            else
            {
                // 빈 슬롯
                var slot = CreateEmptyCodingSlot();
                slot.transform.SetParent(inner.transform, false);
            }
        }

        private static void AppendFlowFooter(Transform parent, Color color)
        {
            var f = NewRect("Footer", 0f, 28f);
            f.transform.SetParent(parent, false);
            AddImage(f, color);
            var le = f.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.flexibleWidth = 1f;
        }

        // ── Logic 체인 (수평) ───────────────────────────────────
        private static void AppendChain(GameObject baseBlock, BlockEntry[] chain, Canvas rootCanvas, bool draggable)
        {
            // baseBlock을 HorizontalLayoutGroup 컨테이너로 감싸기
            var container = NewRect(baseBlock.name + "_Chain", 0f, 56f);
            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childForceExpandWidth = false;
            container.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            baseBlock.transform.SetParent(container.transform, false);

            foreach (var c in chain)
            {
                var child = Create(c, rootCanvas, draggable);
                child.transform.SetParent(container.transform, false);
            }
        }

        // ── 블록 카테고리에 맞는 소켓 전체 부착 (멱등 — 이미 있으면 건너뜀)
        public static void AttachSockets(CodingBlock block)
        {
            if (block.Category == BlockCategory.Command)
                AttachValueOutSocket(block.gameObject, new Vector2(-8f, 11f));
            else if (block.Category == BlockCategory.Value)
                AttachValueInSocket(block.gameObject, new Vector2(8f, 0f));

            var cat = block.Category;
            if (cat != BlockCategory.Value && cat != BlockCategory.Logic)
            {
                bool isStart   = block.gameObject.name == "시작하기";
                bool isEnd     = block.gameObject.name == "종료하기";
                bool isCommand = cat == BlockCategory.Command;

                var outOffset = isStart   ? new Vector2(-69f, 8f)   :
                                isCommand ? new Vector2(-78f, 4f)   : Vector2.zero;
                var inOffset  = isEnd     ? new Vector2(-73f, -20f) :
                                isCommand ? new Vector2(-78f, -16f) : Vector2.zero;

                if (!isEnd)   AttachOutSocket(block.gameObject, outOffset);
                if (!isStart) AttachInSocket(block.gameObject,  inOffset);
            }
        }

        // ── 블록에 ChainOutSocket 후부착 — 앵커 (0.5, 0) 하단 중앙
        public static void AttachOutSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find("ChainOutSocket") != null) return;

            var go = new GameObject("ChainOutSocket");
            go.transform.SetParent(block.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<ChainOutSocket>();
        }

        // ── 블록에 ChainInSocket 후부착 — 앵커 (0.5, 1) 상단 중앙
        public static void AttachInSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find("ChainInSocket") != null) return;

            var go = new GameObject("ChainInSocket");
            go.transform.SetParent(block.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<ChainInSocket>();
        }

        // ── Command 블록에 ValueOutSocket 후부착 — 앵커 (1, 0.5) 우측 중앙
        public static void AttachValueOutSocket(GameObject commandBlock, Vector2 offset = default)
        {
            if (commandBlock.transform.Find("ValueOutSocket") != null) return;

            var go = new GameObject("ValueOutSocket");
            go.transform.SetParent(commandBlock.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<ValueOutSocket>();
        }

        // ── Value 블록에 ValueInSocket 후부착 — 앵커 (0, 0.5) 좌측 중앙
        public static void AttachValueInSocket(GameObject valueBlock, Vector2 offset = default)
        {
            if (valueBlock.transform.Find("ValueInSocket") != null) return;

            var go = new GameObject("ValueInSocket");
            go.transform.SetParent(valueBlock.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<ValueInSocket>();
        }

        // ── 빈 CodingSlot ───────────────────────────────────────
        public static GameObject CreateEmptyCodingSlot()
        {
            var go = NewRect("Slot", 0f, 60f);
            AddImage(go, new Color(1f, 1f, 1f, 0.08f));
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 60f;
            le.flexibleWidth = 1f;
            go.AddComponent<CodingSlot>();
            return go;
        }

        // ── 아웃라인 오버레이 머티리얼 3종 ──────────────────────────
        // Full: 전체 범위 (에러 표시)
        // Bottom: 하단 35% (체인 스냅 표시)
        // Right: 우측 20% (값 스냅 표시)
        private static Material _spriteFillMaterial;
        private static Material _spriteFillMaterialBottom;
        private static Material _spriteFillMaterialRight;

        private static Material SpriteFillMaterial
        {
            get
            {
                if (_spriteFillMaterial != null) return _spriteFillMaterial;
                _spriteFillMaterial = MakeSpriteFillMat("BlockOutlineFull", 1.0f, 0.0f);
                return _spriteFillMaterial;
            }
        }

        private static Material SpriteFillMaterialBottom
        {
            get
            {
                if (_spriteFillMaterialBottom != null) return _spriteFillMaterialBottom;
                _spriteFillMaterialBottom = MakeSpriteFillMat("BlockOutlineBottom", 0.35f, 0.0f);
                return _spriteFillMaterialBottom;
            }
        }

        private static Material SpriteFillMaterialRight
        {
            get
            {
                if (_spriteFillMaterialRight != null) return _spriteFillMaterialRight;
                _spriteFillMaterialRight = MakeSpriteFillMat("BlockOutlineRight", 1.0f, 0.8f);
                return _spriteFillMaterialRight;
            }
        }

        private static Material MakeSpriteFillMat(string matName, float yMax, float xMin)
        {
            var shader = Shader.Find("Custom/UI/SpriteFill");
            if (shader == null)
            {
                Debug.LogWarning("[BlockFactory] 'Custom/UI/SpriteFill' 셰이더를 찾을 수 없습니다.");
                return null;
            }
            var mat = new Material(shader) { name = matName };
            mat.SetFloat("_YMax", yMax);
            mat.SetFloat("_XMin", xMin);
            return mat;
        }

        // ── 공통 유틸 ───────────────────────────────────────────
        private static GameObject NewRect(string name, float w, float h)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        // isBlockBody=true: go에 Image를 달지 않고 자식 두 개로 분리.
        // 렌더링 순서 — sibling 0(아웃라인) → sibling 1(주 이미지) → sibling 2+(레이블·소켓)
        private static Image AddImage(GameObject go, Color color, Sprite sprite = null, bool isBlockBody = false)
        {
            if (!isBlockBody)
            {
                // 내부 구조 이미지 (InnerContainer, Footer 등): go에 직접 Image 추가
                var img = go.AddComponent<Image>();
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = false;
                    img.color = Color.white;
                    if (go.TryGetComponent<RectTransform>(out var rt))
                        rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
                }
                else
                {
                    img.color = color;
                }
                return img;
            }

            // ── 블록 본체 ─────────────────────────────────────────────────────
            if (sprite != null && go.TryGetComponent<RectTransform>(out var goRt))
                goRt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);

            if (sprite != null)
            {
                // sibling 0~2: 방향별 하이라이트 오버레이 (기본 투명)
                AddHighlightOverlays(go, sprite);

                // sibling 1: 주 스프라이트 (go 전체를 덮음)
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                var srt = spriteGo.AddComponent<RectTransform>();
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = srt.offsetMax = Vector2.zero;
                var spriteImg = spriteGo.AddComponent<Image>();
                spriteImg.sprite = sprite;
                spriteImg.type = Image.Type.Simple;
                spriteImg.preserveAspect = false;
                spriteImg.color = Color.white;
                return spriteImg;
            }
            else
            {
                // sibling 0: 흰 테두리 오버레이 (2px 확대 흰 사각형)
                AddBorderOverlay(go);

                // sibling 1: 주 색상 (go 전체를 덮음)
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
                var frt = fillGo.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.color = color;
                return fillImg;
            }
        }

        // 스프라이트 블록용 방향별 하이라이트 오버레이 3종 생성 (기본 투명, 런타임에 색 변경)
        // sibling 0: SpriteOutline  — 전체 (컴파일 에러 → 빨간색)
        // sibling 1: ChainHighlight — 셰이더로 하단 35%만 표시 (체인 스냅 → 초록색)
        // sibling 2: ValueHighlight — 셰이더로 우측 20%만 표시 (값 스냅 → 초록색)
        private static void AddHighlightOverlays(GameObject blockGo, Sprite sprite)
        {
            const float t = 2f;
            var minOff = new Vector2(-t, -t);
            var maxOff = new Vector2( t,  t);

            AddOverlay("SpriteOutline",  blockGo, sprite, Vector2.zero, Vector2.one, minOff, maxOff, SpriteFillMaterial);
            AddOverlay("ChainHighlight", blockGo, sprite, Vector2.zero, Vector2.one, minOff, maxOff, SpriteFillMaterialBottom);
            AddOverlay("ValueHighlight", blockGo, sprite, Vector2.zero, Vector2.one, minOff, maxOff, SpriteFillMaterialRight);
        }

        private static void AddOverlay(string name, GameObject parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Material mat = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.sprite         = sprite;
            img.type           = Image.Type.Simple;
            img.preserveAspect = false;
            img.color          = Color.clear;
            img.raycastTarget  = false;
            var useMat = mat ?? SpriteFillMaterial;
            if (useMat != null) img.material = useMat;
        }

        // 스프라이트 없는 블록 본체용 테두리 (기본 투명, 런타임에 색 변경)
        private static void AddBorderOverlay(GameObject go, float thickness = 2f)
        {
            var border = new GameObject("SpriteOutline");
            border.transform.SetParent(go.transform, false);
            border.transform.SetAsFirstSibling();
            var rt = border.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-thickness, -thickness);
            rt.offsetMax = new Vector2( thickness,  thickness);
            var img = border.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = false;
        }

        private static void AddLabel(GameObject go, string text, int size = 28)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var t = new GameObject("Label");
            t.transform.SetParent(go.transform, false);
            var rt = t.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = t.AddComponent<Text>();
            txt.text = text;
            txt.font = font;
            txt.fontSize = size;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        private static void AddDraggable(GameObject go, BlockCategory cat, Canvas rootCanvas)
        {
            go.TryGetComponent<RectTransform>(out var brt);
            brt.pivot = new Vector2(0f, 0.5f);
            var block = go.AddComponent<CodingBlock>();
            block.Init(cat, rootCanvas);
        }

    }
}