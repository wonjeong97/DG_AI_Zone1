using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace DG.Game
{
    public static class BlockFactory
    {
        private const string BlockImagePath = "Images/Blocks/";

        // 이름별로 1회만 Addressables에서 로드하고 이후에는 캐시에서 반환
        private readonly static Dictionary<string, Sprite> _spriteCache = new();

        // 카테고리 + controlRole(Start/End 구분)로 스프라이트 로드
        private static async UniTask<Sprite> LoadSpriteAsync(BlockCategory cat, ControlRole controlRole = ControlRole.None)
        {
            string name = cat switch
            {
                BlockCategory.Control => controlRole == ControlRole.Start ? "Start" : "End",
                BlockCategory.Command => "Command",
                BlockCategory.Value => "Value",
                BlockCategory.FlowControl => "FlowControl",
                BlockCategory.ConditionAction => "ConditionAction",
                BlockCategory.Action => "Action",
                BlockCategory.Logic => "Logic",
                _ => null
            };
            if (name is null) return null;

            if (_spriteCache.TryGetValue(name, out Sprite cached)) return cached;

            Sprite sprite = await Addressables.LoadAssetAsync<Sprite>(name);
            _spriteCache[name] = sprite;
            return sprite;
        }

        // 한글 라벨용 폰트 — 1회만 로드 후 캐시.
        private static UnityEngine.TextCore.Text.FontAsset _labelFont;

        public static async UniTask<UnityEngine.TextCore.Text.FontAsset> LoadLabelFontAsync()
        {
            if (_labelFont) return _labelFont;
            _labelFont = Resources.Load<UnityEngine.TextCore.Text.FontAsset>("Fonts & Materials/GamtanRoadTantan SDF");
            if (!_labelFont)
            {
                // Resources 로드 실패 시 어드레서블로 2차 시도
                _labelFont = await Addressables.LoadAssetAsync<UnityEngine.TextCore.Text.FontAsset>("GamtanRoadTantan SDF");
            }
            return _labelFont;
        }

        /// <summary>
        /// 미구현 스프라이트 대체를 위한 기본 색상 반환.
        /// </summary>
        public static Color GetColor(BlockCategory cat) => cat switch
        {
            BlockCategory.Control => new Color(0.20f, 0.20f, 0.20f),
            BlockCategory.Command => new Color32(213, 96, 180, 255),
            BlockCategory.Value => new Color(0.27f, 0.60f, 0.85f),
            BlockCategory.FlowControl => new Color(0.92f, 0.57f, 0.08f),
            BlockCategory.ConditionAction => new Color(0.86f, 0.40f, 0.63f),
            BlockCategory.Action => new Color(0.28f, 0.72f, 0.66f),
            BlockCategory.Logic => new Color(0.33f, 0.73f, 0.36f),
            BlockCategory.Condition => new Color(0.60f, 0.40f, 0.80f),
            _ => Color.white
        };

        /// <summary>
        /// 카테고리 선택 버튼에 표시할 한글 이름. (한 곳에서 관리 — 필요 시 이 매핑만 수정)
        /// </summary>
        public static string GetCategoryName(BlockCategory cat) => cat switch
        {
            BlockCategory.Control => "제어",
            BlockCategory.Command => "동작",
            BlockCategory.Value => "변수",
            BlockCategory.FlowControl => "반복",
            BlockCategory.ConditionAction => "조건 동작",
            BlockCategory.Action => "행동",
            BlockCategory.Logic => "논리",
            BlockCategory.Condition => "조건",
            _ => cat.ToString()
        };

        // ── 진입점 ──────────────────────────────────────────────
        public static async UniTask<GameObject> Create(BlockEntry entry, Canvas rootCanvas, bool draggable = true)
        {
            return entry.category switch
            {
                BlockCategory.Command     => await CreateCommandBlock(entry, rootCanvas, draggable),
                BlockCategory.FlowControl => await CreateFlowBlock(entry, rootCanvas, draggable),
                BlockCategory.Logic       => await CreateLogicBlock(entry, rootCanvas, draggable),
                _                         => await CreateSimpleBlock(entry, rootCanvas, draggable)
            };
        }

        // ── 단순 블록 ───────────────────────────────────────────
        private static async UniTask<GameObject> CreateSimpleBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(entry.category, entry.controlRole);
            
            float w = 220f;
            float h = 56f;
            if (entry.category == BlockCategory.Control)
            {
                // 원래 리소스 블록의 크기를 따라가기 위한 고정 크기 설정
                if (entry.controlRole == ControlRole.Start)
                {
                    w = 353f;
                    h = 127f;
                }
                else
                {
                    w = 353f;
                    h = 104f;
                }
            }
            else if (entry.category == BlockCategory.Value)
            {
                // 원래 리소스 블록의 크기를 따라가기 위한 고정 크기 설정
                w = 287f;
                h = 89f;
            }

            GameObject go = NewRect(entry.label, w, h);
            AddBlockBody(go, GetColor(entry.category), sprite);
            if (go.TryGetComponent<RectTransform>(out RectTransform goRt))
                goRt.sizeDelta = new Vector2(w, h);

            go.AddComponent<CanvasGroup>();
            if (draggable)
            {
                AddDraggable(go, entry, rootCanvas);
            }

            // 시작하기 스프라이트는 하단 연결부 탓에 텍스트가 처져 보여 바닥을 20px 올림
            await AddLabel(go, entry.label, 28, entry.controlRole == ControlRole.Start ? 20f : 0f);

            // Logic 블록은 수평 체인 슬롯 포함
            if (entry.category == BlockCategory.Logic && entry.chainBlocks is not null)
                await AppendChain(go, entry.chainBlocks, rootCanvas, draggable);

            return go;
        }

        // ── Command 블록 ────────────────────────────────────────
        private static async UniTask<GameObject> CreateCommandBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            Sprite cmdSprite = await LoadSpriteAsync(BlockCategory.Command);
            // 원래 리소스 블록의 크기를 따라가기 위한 고정 크기 설정
            float cmdW = 371f;
            float cmdH = 119f;

            GameObject go = NewRect(entry.label, cmdW, cmdH);
            go.AddComponent<CanvasGroup>();
            if (draggable)
                AddDraggable(go, entry, rootCanvas);

            GameObject labelPart = NewRect("Label", cmdW, cmdH);
            labelPart.transform.SetParent(go.transform, false);
            labelPart.TryGetComponent<RectTransform>(out RectTransform labelRT);
            labelRT.anchorMin = labelRT.anchorMax = labelRT.pivot = new Vector2(0f, 1f);
            labelRT.anchoredPosition = Vector2.zero;
            AddBlockBody(labelPart, GetColor(entry.category), cmdSprite);
            if (labelPart.TryGetComponent<RectTransform>(out RectTransform lpRt))
                lpRt.sizeDelta = new Vector2(cmdW, cmdH);

            await AddLabel(labelPart, entry.label, 24, 10f);

            return go;
        }

        // ── FlowControl 블록 (C자형) ────────────────────────────
        private const float FlowBlockWidth = 254f;
        private const float FlowHeaderHeight = 78f;
        private const float FlowElseHeight = 44f;
        private const float FlowFooterHeight = 83f;

        private static async UniTask<GameObject> CreateFlowBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(BlockCategory.FlowControl);

            GameObject go = NewRect(entry.label, FlowBlockWidth, 0f);
            go.AddComponent<CanvasGroup>();

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Label GO: 다른 블록과 동일하게 아웃라인·하이라이트·배경·텍스트를 한 곳에 통합
            await AppendFlowLabel(go.transform, entry.label, sprite);

            if (draggable)
                AddDraggable(go, entry, rootCanvas);

            // 헤더 높이 스페이서 (VLG용, 시각은 Label GO가 담당)
            AppendFlowSpacer(go.transform, "Header_" + entry.label, FlowHeaderHeight);

            // 조건 슬롯: 헤더 스페이서 우측에 고정
            Transform headerSpacer = go.transform.Find("Header_" + entry.label);
            if (headerSpacer)
            {
                GameObject socketGo = new GameObject("ValueOutSocket");
                socketGo.transform.SetParent(headerSpacer, false);
                RectTransform socketRt = socketGo.AddComponent<RectTransform>();
                socketRt.anchorMin = socketRt.anchorMax = new Vector2(1f, 0.5f);
                socketRt.pivot     = new Vector2(0.5f, 0.5f);
                socketRt.sizeDelta = Vector2.zero;
                socketRt.anchoredPosition = new Vector2(-8f, 0f);
                socketGo.AddComponent<LayoutElement>().ignoreLayout = true;
                socketGo.AddComponent<ValueOutSocket>();
            }

            // 내부 컨테이너
            AppendInnerContainer(go.transform, entry.innerBlocks, rootCanvas, draggable);

            // else 분기 (만약 블록) — 구분 텍스트 포함
            if (entry.elseBlocks is not null && entry.elseBlocks.Length > 0)
            {
                await AppendFlowHeader(go.transform, "아니면", FlowElseHeight);
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
        private static async UniTask AppendFlowLabel(Transform parent, string label, Sprite sprite)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            if (sprite)
            {
                AddHighlightOverlays(go, sprite);

                GameObject bgGo = new GameObject("Background");
                bgGo.transform.SetParent(go.transform, false);
                RectTransform bgRt = bgGo.AddComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
                Image bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = sprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
                bgImg.raycastTarget = false;
            }

            // 텍스트: 상단 FlowHeaderHeight 영역에만 표시
            UnityEngine.TextCore.Text.FontAsset font = await LoadLabelFontAsync();
            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            textRt.sizeDelta = new Vector2(0f, FlowHeaderHeight);
            TMPro.TextMeshProUGUI txt = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = 26;
            txt.color = Color.white;
            txt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        // VLG 높이 스페이서 — 시각 없음, Label GO가 배경·텍스트를 담당
        private static void AppendFlowSpacer(Transform parent, string name, float height)
        {
            GameObject h = NewRect(name, 0f, height);
            h.transform.SetParent(parent, false);
            LayoutElement le = h.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
        }

        // else 구분 헤더 — 스페이서 + 텍스트 (시각은 Label GO의 9-slice 배경이 담당)
        private static async UniTask AppendFlowHeader(Transform parent, string label, float height)
        {
            GameObject h = NewRect("Header_" + label, 0f, height);
            h.transform.SetParent(parent, false);
            await AddLabel(h, label, 26);
            LayoutElement le = h.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
        }

        private const float InnerMinHeight = 50f;

        private static void AppendInnerContainer(Transform parent, BlockEntry[] blocks,
            Canvas rootCanvas, bool draggable)
        {
            GameObject inner = NewRect("Inner", 0f, InnerMinHeight);
            inner.transform.SetParent(parent, false);

            LayoutElement le = inner.AddComponent<LayoutElement>();
            le.preferredHeight = InnerMinHeight;
            le.minHeight = InnerMinHeight;
            le.flexibleWidth = 1f;

            // 진입 소켓: 내부 영역 상단 중앙
            GameObject socketGo = new GameObject("InnerSocket");
            socketGo.transform.SetParent(inner.transform, false);
            RectTransform socketRt = socketGo.AddComponent<RectTransform>();
            socketRt.anchorMin = socketRt.anchorMax = new Vector2(0.5f, 1f);
            socketRt.pivot = new Vector2(0.5f, 0.5f);
            socketRt.sizeDelta = Vector2.zero;
            socketRt.anchoredPosition = new Vector2(-25f, 4.5f);
            InnerSocket innerSocket = socketGo.AddComponent<InnerSocket>();

            // 빈 상태 표시 (블록이 들어오면 숨겨짐)
            GameObject empty = NewRect("EmptyIndicator", 0f, 60f);
            empty.transform.SetParent(inner.transform, false);
            empty.TryGetComponent<RectTransform>(out RectTransform emptyRt);
            emptyRt.anchorMin = new Vector2(0.14f, 0.5f);
            emptyRt.anchorMax = new Vector2(0.97f, 0.5f);
            emptyRt.pivot = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta = new Vector2(0f, 60f);
            emptyRt.anchoredPosition = Vector2.zero;
            Image emptyImg = empty.AddComponent<Image>();
            emptyImg.color = new Color(1f, 1f, 1f, 0.08f);
            innerSocket.SetEmptyIndicator(empty);

            // 체인 하단 기준점: Inner 바닥 중앙 — FlowInnerResize가 마지막 ChainOutSocket과 이 위치를 맞춰 높이를 계산
            GameObject bottomSocketGo = new GameObject("InnerBottomSocket");
            bottomSocketGo.transform.SetParent(inner.transform, false);
            RectTransform bottomRt = bottomSocketGo.AddComponent<RectTransform>();
            bottomRt.anchorMin = bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0.5f);
            bottomRt.sizeDelta = Vector2.zero;
            bottomRt.anchoredPosition = new Vector2(-25f, -25f);
            bottomSocketGo.AddComponent<InnerBottomSocket>();

            // 사전 배치 블록은 현재 미지원 (런타임 드래그로만 배치)
            if (blocks is not null && blocks.Length > 0)
                Debug.LogWarning("[BlockFactory] 사전 배치 innerBlocks는 아직 지원되지 않습니다.");

            inner.AddComponent<FlowInnerResize>();
        }

        private static void AppendFlowFooter(Transform parent)
        {
            GameObject f = NewRect("Footer", 0f, FlowFooterHeight);
            f.transform.SetParent(parent, false);
            LayoutElement le = f.AddComponent<LayoutElement>();
            le.preferredHeight = FlowFooterHeight;
            le.flexibleWidth = 1f;
        }

        // ── Logic 블록 (그리고 / 또는) ───────────────────────────────────
        // 단순 레이블 블록 — ConditionIn/Out 소켓으로 조건 체인에 연결
        private static async UniTask<GameObject> CreateLogicBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(BlockCategory.Logic);
            GameObject go = NewRect(entry.label, 120f, 56f);
            AddBlockBody(go, GetColor(BlockCategory.Logic), sprite);
            go.AddComponent<CanvasGroup>();
            if (draggable)
                AddDraggable(go, entry, rootCanvas);
            await AddLabel(go, entry.label);
            return go;
        }

        // ── Logic 체인 (수평) ───────────────────────────────────
        private static async UniTask AppendChain(GameObject baseBlock, BlockEntry[] chain, Canvas rootCanvas, bool draggable)
        {
            // baseBlock을 HorizontalLayoutGroup 컨테이너로 감싸기
            GameObject container = NewRect(baseBlock.name + "_Chain", 0f, 56f);
            HorizontalLayoutGroup hlg = container.AddComponent<HorizontalLayoutGroup>();
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
                GameObject child = await Create(c, rootCanvas, draggable);
                child.transform.SetParent(container.transform, false);
            }
        }

        /// <summary>
        /// 블록 카테고리에 따른 연결부 소켓 생성 및 배치.
        /// </summary>
        public static void AttachSockets(CodingBlock block)
        {
            var cat = block.Category;

            // Command만 단일 ValueOutSocket — FlowControl은 헤더에 내장, Logic은 두 조건 슬롯 내장
            if (cat == BlockCategory.Command)
                AttachValueOutSocket(block.gameObject, new Vector2(-8f, 11f));

            if (cat == BlockCategory.Value)
                AttachValueInSocket(block.gameObject, new Vector2(16f, 3.5f));
            else if (cat == BlockCategory.Condition || cat == BlockCategory.Logic)
                AttachValueInSocket(block.gameObject, new Vector2(8f, 0f));

            // Condition / Logic 블록: 수평 조건 체인 소켓
            if (cat == BlockCategory.Condition || cat == BlockCategory.Logic)
            {
                AttachConditionInSocket(block.gameObject,  new Vector2(8f, 0f));
                AttachConditionOutSocket(block.gameObject, new Vector2(-8f, 0f));
            }

            if (cat != BlockCategory.Value && cat != BlockCategory.Logic && cat != BlockCategory.Condition)
            {
                bool isStart   = block.ControlRole == ControlRole.Start;
                bool isEnd     = block.ControlRole == ControlRole.End;
                bool isCommand = cat == BlockCategory.Command;
                bool isFlow    = cat == BlockCategory.FlowControl;

                Vector2 outOffset = isStart   ? new Vector2(-68.5f, 16f)   :
                                    isFlow    ? new Vector2(-60f, 3f)      :
                                    isCommand ? new Vector2(-78.3f, 11.5f) : Vector2.zero;
                Vector2 inOffset  = isEnd     ? new Vector2(-73f, -20f)    :
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

            GameObject go = new GameObject("ChainOutSocket");
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
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

            GameObject go = new GameObject("ChainInSocket");
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
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

            GameObject go = new GameObject("ValueOutSocket");
            go.transform.SetParent(commandBlock.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
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

            GameObject go = new GameObject("ValueInSocket");
            go.transform.SetParent(valueBlock.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ValueInSocket>();
        }

        // ── Condition 체인 소켓 ─────────────────────────────────
        public static void AttachConditionOutSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find("ConditionOutSocket")) return;

            GameObject go = new GameObject("ConditionOutSocket");
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ConditionOutSocket>();
        }

        public static void AttachConditionInSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find("ConditionInSocket")) return;

            GameObject go = new GameObject("ConditionInSocket");
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ConditionInSocket>();
        }

        // ── 빈 CodingSlot ───────────────────────────────────────
        public static GameObject CreateEmptyCodingSlot()
        {
            GameObject go = NewRect("Slot", 0f, 60f);
            AddImage(go, new Color(1f, 1f, 1f, 0.08f));
            LayoutElement le = go.AddComponent<LayoutElement>();
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
            Shader shader = Shader.Find("Custom/UI/SpriteFill");
            if (!shader)
            {
                Debug.LogWarning("[BlockFactory] 'Custom/UI/SpriteFill' 셰이더를 찾을 수 없습니다.");
                return null;
            }

            Material mat = new Material(shader) { name = matName };
            mat.SetFloat("_YMax", yMax);
            mat.SetFloat("_XMin", xMin);
            return mat;
        }

        // ── 공통 유틸 ───────────────────────────────────────────
        private static GameObject NewRect(string name, float w, float h)
        {
            GameObject go = new GameObject(name);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        // 내부 구조용 이미지 (InnerContainer, EmptySlot 등): go에 직접 Image 부착
        private static void AddImage(GameObject go, Color color, Sprite sprite = null)
        {
            Image img = go.AddComponent<Image>();
            if (sprite)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;
                if (go.TryGetComponent<RectTransform>(out RectTransform rt))
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
            if (sprite && go.TryGetComponent<RectTransform>(out RectTransform goRt))
                goRt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);

            if (sprite)
            {
                AddHighlightOverlays(go, sprite);

                GameObject spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                RectTransform srt = spriteGo.AddComponent<RectTransform>();
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = srt.offsetMax = Vector2.zero;
                Image spriteImg = spriteGo.AddComponent<Image>();
                spriteImg.sprite = sprite;
                spriteImg.type = Image.Type.Simple;
                spriteImg.preserveAspect = false;
                spriteImg.color = Color.white;
            }
            else
            {
                AddBorderOverlay(go);

                GameObject fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
                RectTransform frt = fillGo.AddComponent<RectTransform>();
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
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.clear;
            img.raycastTarget = false;
            Material useMat = mat ?? SpriteFillMaterial;
            if (useMat) img.material = useMat;
        }

        // 스프라이트 없는 블록 본체용 테두리 (기본 투명, 런타임에 색 변경)
        private static void AddBorderOverlay(GameObject go, float thickness = 2f)
        {
            GameObject border = new GameObject("SpriteOutline");
            border.transform.SetParent(go.transform, false);
            border.transform.SetAsFirstSibling();
            RectTransform rt = border.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-thickness, -thickness);
            rt.offsetMax = new Vector2(thickness, thickness);
            Image img = border.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = false;
        }

        private static async UniTask AddLabel(GameObject go, string text, int size = 28, float bottom = 0f)
        {
            UnityEngine.TextCore.Text.FontAsset font = await LoadLabelFontAsync();
            GameObject t = new GameObject("Label");
            t.transform.SetParent(go.transform, false);
            RectTransform rt = t.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, bottom);
            rt.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI txt = t.AddComponent<TMPro.TextMeshProUGUI>();
            txt.text = text;
            txt.font = font;
            txt.fontSize = size;
            txt.color = Color.white;
            txt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        private static void AddDraggable(GameObject go, BlockEntry entry, Canvas rootCanvas)
        {
            go.TryGetComponent<RectTransform>(out RectTransform brt);
            brt.pivot = new Vector2(0f, entry.category == BlockCategory.FlowControl ? 1f : 0.5f);
            CodingBlock block = go.AddComponent<CodingBlock>();
            block.Init(entry.category, rootCanvas, entry.valueKind, entry.controlRole);
        }
    }
}
