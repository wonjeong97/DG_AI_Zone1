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
            AddImage(go, GetColor(entry.category), sprite);
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
            AddImage(labelPart, GetColor(entry.category), cmdSprite);
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
            AddImage(h, color, sprite);
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

        // ── 공통 유틸 ───────────────────────────────────────────
        private static GameObject NewRect(string name, float w, float h)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static Image AddImage(GameObject go, Color color, Sprite sprite = null)
        {
            var img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;

                // RectTransform을 소스 이미지 크기에 맞춤
                if (go.TryGetComponent<RectTransform>(out var rt))
                    rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            }
            else
            {
                img.color = color;
            }

            return img;
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