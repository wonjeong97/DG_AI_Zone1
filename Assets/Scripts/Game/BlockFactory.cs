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

        /// <summary>
        /// 미구현 스프라이트 대체를 위한 기본 색상 반환.
        /// </summary>
        public static Color GetColor(BlockCategory cat) => cat switch
        {
            BlockCategory.Control => new Color(0.20f, 0.20f, 0.20f),
            BlockCategory.Command => new Color(0.82f, 0.36f, 0.28f),
            BlockCategory.Value => new Color(0.27f, 0.60f, 0.85f),
            BlockCategory.FlowControl => new Color(0.92f, 0.57f, 0.08f),
            BlockCategory.ConditionAction => new Color(0.86f, 0.40f, 0.63f),
            BlockCategory.Action => new Color(0.28f, 0.72f, 0.66f),
            BlockCategory.Logic => new Color(0.33f, 0.73f, 0.36f),
            BlockCategory.Condition => new Color(0.60f, 0.40f, 0.80f),
            _ => Color.white
        };

        // ── 진입점 ──────────────────────────────────────────────
        public static GameObject Create(BlockEntry entry, Canvas rootCanvas, bool draggable = true)
        {
            return entry.category switch
            {
                BlockCategory.Command => CreateCommandBlock(entry, rootCanvas, draggable),
                BlockCategory.FlowControl => CreateFlowBlock(entry, rootCanvas, draggable),
                _ => CreateSimpleBlock(entry, rootCanvas, draggable)
            };
        }

        // ── 단순 블록 ───────────────────────────────────────────
        private static GameObject CreateSimpleBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            var sprite = LoadSprite(entry.category, entry.label);
            var go = NewRect(entry.label, 220f, 56f);
            AddBlockBody(go, GetColor(entry.category), sprite);
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
            float cmdW = cmdSprite?.rect.width ?? 260f;
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
            AddBlockBody(labelPart, GetColor(entry.category), cmdSprite);
            AddLabel(labelPart, entry.label, 24);

            return go;
        }

        // ── FlowControl 블록 (C자형) ────────────────────────────
        private const float FlowBlockWidth = 254f;
        private const float FlowHeaderHeight = 78f;
        private const float FlowElseHeight = 44f;
        private const float FlowFooterHeight = 83f;

        private static GameObject CreateFlowBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            var sprite = LoadSprite(BlockCategory.FlowControl);

            var go = NewRect(entry.label, FlowBlockWidth, 0f);
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
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Label GO: 다른 블록과 동일하게 아웃라인·하이라이트·배경·텍스트를 한 곳에 통합
            AppendFlowLabel(go.transform, entry.label, sprite);

            if (draggable)
                AddDraggable(go, entry.category, rootCanvas);

            // 헤더 높이 스페이서 (VLG용, 시각은 Label GO가 담당)
            AppendFlowSpacer(go.transform, "Header_" + entry.label, FlowHeaderHeight);

            // 내부 컨테이너
            AppendInnerContainer(go.transform, entry.innerBlocks, rootCanvas, draggable);

            // else 분기 (만약 블록) — 구분 텍스트 포함
            if (entry.elseBlocks != null && entry.elseBlocks.Length > 0)
            {
                AppendFlowHeader(go.transform, "아니면", FlowElseHeight);
                AppendInnerContainer(go.transform, entry.elseBlocks, rootCanvas, draggable);
            }

            // 푸터 높이 스페이서
            AppendFlowFooter(go.transform);

            return go;
        }

        // ── FlowControl 헬퍼 ────────────────────────────────────

        // 다른 블록의 Label GO와 동일한 구조:
        // 0~2: SpriteOutline / ChainHighlight / ValueHighlight
        //   3: Background (9-slice, 블록 전체 커버)
        //   4: Label 텍스트 (헤더 영역 상단에 고정)
        // ignoreLayout=true → VLG 배치에서 제외, 블록 전체를 덮는 시각 레이어로만 동작
        private static void AppendFlowLabel(Transform parent, string label, Sprite sprite)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            if (sprite)
            {
                AddHighlightOverlays(go, sprite);

                var bgGo = new GameObject("Background");
                bgGo.transform.SetParent(go.transform, false);
                var bgRt = bgGo.AddComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = sprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
                bgImg.raycastTarget = false;
            }

            // 텍스트: 상단 FlowHeaderHeight 영역에만 표시
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            textRt.sizeDelta = new Vector2(0f, FlowHeaderHeight);
            var txt = textGo.AddComponent<Text>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = 26;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        // VLG 높이 스페이서 — 시각 없음, Label GO가 배경·텍스트를 담당
        private static void AppendFlowSpacer(Transform parent, string name, float height)
        {
            var h = NewRect(name, 0f, height);
            h.transform.SetParent(parent, false);
            var le = h.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
        }

        // else 구분 헤더 — 스페이서 + 텍스트 (시각은 Label GO의 9-slice 배경이 담당)
        private static void AppendFlowHeader(Transform parent, string label, float height)
        {
            var h = NewRect("Header_" + label, 0f, height);
            h.transform.SetParent(parent, false);
            AddLabel(h, label, 26);
            var le = h.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
        }

        private const float InnerMinHeight = 50f;

        private static void AppendInnerContainer(Transform parent, BlockEntry[] blocks,
            Canvas rootCanvas, bool draggable)
        {
            var inner = NewRect("Inner", 0f, InnerMinHeight);
            inner.transform.SetParent(parent, false);

            var le = inner.AddComponent<LayoutElement>();
            le.preferredHeight = InnerMinHeight;
            le.minHeight = InnerMinHeight;
            le.flexibleWidth = 1f;

            // 진입 소켓: 내부 영역 상단 중앙
            var socketGo = new GameObject("InnerSocket");
            socketGo.transform.SetParent(inner.transform, false);
            var socketRt = socketGo.AddComponent<RectTransform>();
            socketRt.anchorMin = socketRt.anchorMax = new Vector2(0.5f, 1f);
            socketRt.pivot = new Vector2(0.5f, 0.5f);
            socketRt.sizeDelta = Vector2.zero;
            socketRt.anchoredPosition = new Vector2(-25f, 4.5f);
            var innerSocket = socketGo.AddComponent<InnerSocket>();

            // 빈 상태 표시 (블록이 들어오면 숨겨짐)
            var empty = NewRect("EmptyIndicator", 0f, 60f);
            empty.transform.SetParent(inner.transform, false);
            empty.TryGetComponent<RectTransform>(out var emptyRt);
            emptyRt.anchorMin = new Vector2(0.14f, 0.5f);
            emptyRt.anchorMax = new Vector2(0.97f, 0.5f);
            emptyRt.pivot = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta = new Vector2(0f, 60f);
            emptyRt.anchoredPosition = Vector2.zero;
            var emptyImg = empty.AddComponent<Image>();
            emptyImg.color = new Color(1f, 1f, 1f, 0.08f);
            innerSocket.SetEmptyIndicator(empty);

            // 체인 하단 기준점: Inner 바닥 중앙 — FlowInnerResize가 마지막 ChainOutSocket과 이 위치를 맞춰 높이를 계산
            var bottomSocketGo = new GameObject("InnerBottomSocket");
            bottomSocketGo.transform.SetParent(inner.transform, false);
            var bottomRt = bottomSocketGo.AddComponent<RectTransform>();
            bottomRt.anchorMin = bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0.5f);
            bottomRt.sizeDelta = Vector2.zero;
            bottomRt.anchoredPosition = new Vector2(-25f, -25f);
            bottomSocketGo.AddComponent<InnerBottomSocket>();

            // 사전 배치 블록은 현재 미지원 (런타임 드래그로만 배치)
            if (blocks != null && blocks.Length > 0)
                Debug.LogWarning("[BlockFactory] 사전 배치 innerBlocks는 아직 지원되지 않습니다.");

            inner.AddComponent<FlowInnerResize>();
        }

        private static void AppendFlowFooter(Transform parent)
        {
            var f = NewRect("Footer", 0f, FlowFooterHeight);
            f.transform.SetParent(parent, false);
            var le = f.AddComponent<LayoutElement>();
            le.preferredHeight = FlowFooterHeight;
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

        /// <summary>
        /// 블록 카테고리에 따른 연결부 소켓 생성 및 배치.
        /// </summary>
        public static void AttachSockets(CodingBlock block)
        {
            var cat = block.Category;

            if (cat == BlockCategory.Command || cat == BlockCategory.Condition || cat == BlockCategory.Logic || cat == BlockCategory.FlowControl)
                AttachValueOutSocket(block.gameObject, new Vector2(-8f, 11f));

            if (cat == BlockCategory.Value || cat == BlockCategory.Condition || cat == BlockCategory.Logic)
                AttachValueInSocket(block.gameObject, new Vector2(8f, 0f));

            if (cat != BlockCategory.Value && cat != BlockCategory.Logic && cat != BlockCategory.Condition)
            {
                bool isStart   = block.gameObject.name == "시작하기";
                bool isEnd     = block.gameObject.name == "종료하기";
                bool isCommand = cat == BlockCategory.Command;
                bool isFlow    = cat == BlockCategory.FlowControl;

                var outOffset = isStart   ? new Vector2(-69f, 8f)      :
                    isFlow    ? new Vector2(-60f, 3f)      :
                    isCommand ? new Vector2(-78f, 4f)      : Vector2.zero;
                var inOffset  = isEnd     ? new Vector2(-73f, -20f)    :
                    isFlow    ? new Vector2(-55.5f, -17.5f):
                    isCommand ? new Vector2(-78f, -16f)    : Vector2.zero;

                if (!isEnd)   AttachOutSocket(block.gameObject, outOffset);
                if (!isStart) AttachInSocket(block.gameObject,  inOffset);
            }
        }

        // ── 블록에 ChainOutSocket 후부착 — 앵커 (0.5, 0) 하단 중앙
        public static void AttachOutSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find("ChainOutSocket")) return;

            var go = new GameObject("ChainOutSocket");
            go.transform.SetParent(block.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ChainOutSocket>();
        }

        // ── 블록에 ChainInSocket 후부착 — 앵커 (0.5, 1) 상단 중앙
        public static void AttachInSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find("ChainInSocket")) return;

            var go = new GameObject("ChainInSocket");
            go.transform.SetParent(block.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ChainInSocket>();
        }

        // ── Command 블록에 ValueOutSocket 후부착 — 앵커 (1, 0.5) 우측 중앙
        public static void AttachValueOutSocket(GameObject commandBlock, Vector2 offset = default)
        {
            if (commandBlock.transform.Find("ValueOutSocket")) return;

            var go = new GameObject("ValueOutSocket");
            go.transform.SetParent(commandBlock.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ValueOutSocket>();
        }

        // ── Value 블록에 ValueInSocket 후부착 — 앵커 (0, 0.5) 좌측 중앙
        public static void AttachValueInSocket(GameObject valueBlock, Vector2 offset = default)
        {
            if (valueBlock.transform.Find("ValueInSocket")) return;

            var go = new GameObject("ValueInSocket");
            go.transform.SetParent(valueBlock.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
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

        private static Material SpriteFillMaterial       => GetOrLoadMaterial(ref _spriteFillMaterial,       "BlockOutlineFull",   1.0f, 0.0f);
        private static Material SpriteFillMaterialBottom => GetOrLoadMaterial(ref _spriteFillMaterialBottom, "BlockOutlineBottom", 0.35f, 0.0f);
        private static Material SpriteFillMaterialRight  => GetOrLoadMaterial(ref _spriteFillMaterialRight,  "BlockOutlineRight",  1.0f, 0.8f);

        private static Material GetOrLoadMaterial(ref Material cache, string matName, float yMax, float xMin)
        {
            if (!cache) cache = MakeSpriteFillMat(matName, yMax, xMin);
            return cache;
        }

        private static Material MakeSpriteFillMat(string matName, float yMax, float xMin)
        {
            var shader = Shader.Find("Custom/UI/SpriteFill");
            if (!shader)
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

        // 내부 구조용 이미지 (InnerContainer, EmptySlot 등): go에 직접 Image 부착
        private static void AddImage(GameObject go, Color color, Sprite sprite = null)
        {
            var img = go.AddComponent<Image>();
            if (sprite)
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
        }

        // 블록 본체용 시각 계층 구성.
        // 렌더링 순서 — sibling 0~2(하이라이트) → sibling 3(주 이미지) → sibling 4+(레이블·소켓)
        private static void AddBlockBody(GameObject go, Color color, Sprite sprite)
        {
            if (sprite && go.TryGetComponent<RectTransform>(out var goRt))
                goRt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);

            if (sprite)
            {
                AddHighlightOverlays(go, sprite);

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
            }
            else
            {
                AddBorderOverlay(go);

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
                var frt = fillGo.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
                fillGo.AddComponent<Image>().color = color;
            }
        }

        // 스프라이트 블록용 방향별 하이라이트 오버레이 3종 생성 (기본 투명, 런타임에 색 변경)
        // sibling 0: SpriteOutline  — 전체 (컴파일 에러 → 빨간색)
        // sibling 1: ChainHighlight — 셰이더로 하단 35%만 표시 (체인 스냅 → 초록색)
        // sibling 2: ValueHighlight — 셰이더로 우측 20%만 표시 (값 스냅 → 초록색)
        private static void AddHighlightOverlays(GameObject blockGo, Sprite sprite)
        {
            const float t = 4f;
            var minOff = new Vector2(-t, -t);
            var maxOff = new Vector2(t, t);

            AddOverlay("SpriteOutline", blockGo, sprite, Vector2.zero, Vector2.one, minOff, maxOff, SpriteFillMaterial);
            AddOverlay("ChainHighlight", blockGo, sprite, Vector2.zero, Vector2.one, minOff, maxOff,
                SpriteFillMaterialBottom);
            AddOverlay("ValueHighlight", blockGo, sprite, Vector2.zero, Vector2.one, minOff, maxOff,
                SpriteFillMaterialRight);
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
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.clear;
            img.raycastTarget = false;
            var useMat = mat ?? SpriteFillMaterial;
            if (useMat) img.material = useMat;
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
            rt.offsetMax = new Vector2(thickness, thickness);
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
            brt.pivot = new Vector2(0f, cat == BlockCategory.FlowControl ? 1f : 0.5f);
            var block = go.AddComponent<CodingBlock>();
            block.Init(cat, rootCanvas);
        }
    }
}