using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace Game
{
    public static class BlockFactory
    {
        /// <summary>
        /// 블록 생성에 공통으로 필요한 드래그 기준 캔버스, 주입용 리졸버, 로거 묶음.
        /// 재귀 생성(Logic 체인 등)까지 같은 값을 넘기기 위해 한 번에 전달한다.
        /// </summary>
        public readonly struct BuildContext
        {
            public readonly Canvas RootCanvas;
            public readonly IObjectResolver Resolver;
            public readonly Microsoft.Extensions.Logging.ILogger Logger;

            /// <summary>
            /// 생성 컨텍스트를 만든다.
            /// </summary>
            public BuildContext(Canvas rootCanvas, IObjectResolver resolver, Microsoft.Extensions.Logging.ILogger logger)
            {
                RootCanvas = rootCanvas;
                Resolver = resolver;
                Logger = logger;
            }
        }

        // 이름별로 1회만 Addressables에서 로드하고 이후에는 캐시에서 반환
        private readonly static Dictionary<string, Sprite> _spriteCache = new();

        /// <summary>
        /// 카테고리와 controlRole(Start/End 구분)에 맞는 블록 스프라이트를 로드한다 (캐시 우선).
        /// </summary>
        private static async UniTask<Sprite> LoadSpriteAsync(BlockCategory cat, ControlRole controlRole = ControlRole.None)
        {
            string name = cat switch
            {
                BlockCategory.Control         => controlRole == ControlRole.Start
                                                 ? Constants.BlockAssets.StartSprite
                                                 : Constants.BlockAssets.EndSprite,
                BlockCategory.Command         => Constants.BlockAssets.CommandSprite,
                BlockCategory.Value           => Constants.BlockAssets.ValueSprite,
                BlockCategory.FlowControl     => Constants.BlockAssets.FlowControlSprite,
                BlockCategory.ConditionAction => Constants.BlockAssets.ConditionActionSprite,
                BlockCategory.Action          => Constants.BlockAssets.ActionSprite,
                BlockCategory.Logic           => Constants.BlockAssets.LogicSprite,
                BlockCategory.Else            => Constants.BlockAssets.ElseSprite,
                _ => null
            };
            if (name is null) return null;

            if (_spriteCache.TryGetValue(name, out Sprite cached)) return cached;

            Sprite sprite = await Addressables.LoadAssetAsync<Sprite>(name);
            _spriteCache[name] = sprite;
            return sprite;
        }

        // 한글 라벨용 폰트 — Addressables로 로드 후 캐시.
        private static TMPro.TMP_FontAsset _labelFont;

        /// <summary>
        /// 블록 라벨용 한글 폰트를 로드한다 (최초 1회 후 캐시).
        /// </summary>
        public static async UniTask<TMPro.TMP_FontAsset> LoadLabelFontAsync()
        {
            if (_labelFont) return _labelFont;
            _labelFont = await Addressables.LoadAssetAsync<TMPro.TMP_FontAsset>(Constants.ResourcePaths.LabelFontKey);
            return _labelFont;
        }

        /// <summary>
        /// 카테고리 버튼 및 스프라이트 미지정 블록의 대체 색상을 반환한다. (색상 값은 Constants.CategoryColors에서 관리)
        /// </summary>
        public static Color GetColor(BlockCategory cat) => cat switch
        {
            BlockCategory.Control => Constants.CategoryColors.Control,
            BlockCategory.Command => Constants.CategoryColors.Command,
            BlockCategory.Value => Constants.CategoryColors.Value,
            BlockCategory.FlowControl => Constants.CategoryColors.FlowControl,
            BlockCategory.ConditionAction => Constants.CategoryColors.ConditionAction,
            BlockCategory.Action => Constants.CategoryColors.Action,
            BlockCategory.Logic => Constants.CategoryColors.Logic,
            BlockCategory.Condition => Constants.CategoryColors.Condition,
            BlockCategory.Function => Constants.CategoryColors.Function,
            BlockCategory.FunctionDef => Constants.CategoryColors.FunctionDef,
            BlockCategory.Else => Constants.CategoryColors.Else,
            _ => Constants.CategoryColors.Default
        };

        /// <summary>
        /// 카테고리 선택 버튼에 표시할 한글 이름을 반환한다. (한 곳에서 관리 — 필요 시 이 매핑만 수정)
        /// </summary>
        public static string GetCategoryName(BlockCategory cat) => cat switch
        {
            BlockCategory.Control => Constants.CategoryNames.Control,
            BlockCategory.Command => Constants.CategoryNames.Command,
            BlockCategory.Value => Constants.CategoryNames.Value,
            BlockCategory.FlowControl => Constants.CategoryNames.FlowControl,
            BlockCategory.ConditionAction => Constants.CategoryNames.ConditionAction,
            BlockCategory.Action => Constants.CategoryNames.Action,
            BlockCategory.Logic => Constants.CategoryNames.Logic,
            BlockCategory.Condition => Constants.CategoryNames.Condition,
            BlockCategory.Function => Constants.CategoryNames.Function,
            BlockCategory.FunctionDef => Constants.CategoryNames.FunctionDef,
            BlockCategory.Else => Constants.CategoryNames.Else,
            _ => cat.ToString()
        };

        /// <summary>
        /// 인벤토리 선택 탭 그룹을 반환한다. 기능적으로 다른 카테고리를 하나의 탭으로 묶는다.
        /// (함수 사용/구현은 별도 카테고리지만 '함수' 탭 하나로 표시)
        /// </summary>
        public static BlockCategory GetTabCategory(BlockCategory cat) => cat switch
        {
            BlockCategory.FunctionDef => BlockCategory.Function,
            BlockCategory.Else        => BlockCategory.FlowControl,
            _ => cat
        };

        // ── 진입점 ──────────────────────────────────────────────

        /// <summary>
        /// 블록 항목 하나를 카테고리에 맞는 방식(프리팹 또는 코드 조립)으로 생성하고 주입까지 마친다.
        /// </summary>
        public static async UniTask<GameObject> Create(BlockEntry entry, BuildContext ctx, bool draggable = true)
        {
            GameObject go = entry.category switch
            {
                BlockCategory.Value       => await CreateFromPrefab(Constants.BlockAssets.ValuePrefab, entry, ctx, draggable),
                // 조건 블록 — None: 이벤트형 조건(전용 아트) / kind 지정: 값형 조건(Value 아트 재사용)
                BlockCategory.Condition   => await CreateFromPrefab(entry.valueKind == ValueKind.None
                    ? Constants.BlockAssets.ConditionPrefab
                    : Constants.BlockAssets.ValuePrefab, entry, ctx, draggable),
                // Command + ValueKind.None = 값 슬롯 없는 동작 블록 (전용 아트, ValueOutSocket 미부착)
                BlockCategory.Command     => await CreateFromPrefab(entry.valueKind == ValueKind.None
                    ? Constants.BlockAssets.CommandNoValuePrefab
                    : Constants.BlockAssets.CommandPrefab, entry, ctx, draggable),
                BlockCategory.Control     => await CreateFromPrefab(entry.controlRole == ControlRole.Start
                    ? Constants.BlockAssets.StartPrefab
                    : Constants.BlockAssets.EndPrefab, entry, ctx, draggable),
                // 함수 블록 — CommandNoValue와 동일한 구조(값 슬롯 없음, 체인 소켓만)
                BlockCategory.Function    => await CreateFromPrefab(Constants.BlockAssets.FunctionPrefab, entry, ctx, draggable),
                // 함수 구현 블록 — FlowControl과 동일한 C자 컨테이너(헤더 값 슬롯 없음)
                BlockCategory.FunctionDef => await CreateFromPrefab(Constants.BlockAssets.FuncDefPrefab, entry, ctx, draggable),
                BlockCategory.FlowControl => await CreateFlowBlock(entry, ctx, draggable),
                BlockCategory.Logic       => await CreateLogicBlock(entry, ctx, draggable),
                _                         => await CreateSimpleBlock(entry, ctx, draggable)
            };

            // 인벤토리에서 최초 드래그 시에도 정확한 소켓 위치(스냅 기준점)를 기준으로 탐색되도록 소켓을 미리 부착
            if (go && draggable && go.TryGetComponent<CodingBlock>(out CodingBlock block))
            {
                AttachSockets(block);
            }

            return go;
        }

        // ── 프리팹 기반 블록 (Value / Command) ──────────────────
        // 시각 계층(배경·하이라이트·라벨)은 프리팹이 담당하고, 코드에서는 라벨 텍스트와
        // CodingBlock 메타(카테고리/ValueKind) 및 소켓을 주입한다.
        private readonly static Dictionary<string, GameObject> _prefabCache = new();

        /// <summary>
        /// 이름으로 블록 프리팹을 어드레서블에서 1회 로드하고 캐시에서 반환한다.
        /// </summary>
        private static async UniTask<GameObject> LoadPrefabAsync(string name)
        {
            if (_prefabCache.TryGetValue(name, out GameObject cached)) return cached;

            GameObject prefab = await Addressables.LoadAssetAsync<GameObject>(name);
            _prefabCache[name] = prefab;
            return prefab;
        }

        /// <summary>
        /// 프리팹을 리졸버로 인스턴스화(주입 포함)하고 라벨 텍스트와 CodingBlock 메타를 설정해 블록을 생성한다.
        /// </summary>
        private static async UniTask<GameObject> CreateFromPrefab(string prefabName, BlockEntry entry, BuildContext ctx, bool draggable)
        {
            GameObject prefab = await LoadPrefabAsync(prefabName);
            GameObject go = ctx.Resolver.Instantiate(prefab);
            go.name = entry.label; // 컴파일러/채점이 블록 이름으로 값을 읽으므로 라벨과 일치시킨다

            if (!go.TryGetComponent<CodingBlock>(out CodingBlock block))
            {
                if (ctx.Logger != null) ctx.Logger.ZLogWarning($"[BlockFactory] {prefabName} 프리팹에 CodingBlock이 없습니다.");
                return go;
            }

            if (block.LabelText)
                block.LabelText.text = entry.label;
            else if (ctx.Logger != null)
                ctx.Logger.ZLogWarning($"[BlockFactory] {prefabName} 프리팹의 CodingBlock에 라벨 TMP가 연결되지 않았습니다.");

            if (draggable)
                block.Init(entry.category, ctx.RootCanvas, entry.valueKind, entry.controlRole);
            else
                block.enabled = false;

            return go;
        }

        // ── 단순 블록 ───────────────────────────────────────────

        /// <summary>
        /// 스프라이트 본체와 라벨만 있는 블록을 코드로 조립한다.
        /// </summary>
        private static async UniTask<GameObject> CreateSimpleBlock(BlockEntry entry, BuildContext ctx, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(entry.category, entry.controlRole);

            // 아니면 — CommandNoValue와 동일한 크기(값 슬롯 없는 체인 블록)
            bool isElse = entry.category == BlockCategory.Else;
            float w = isElse ? Constants.Blocks.CommandNoValueWidth  : Constants.Blocks.DefaultWidth;
            float h = isElse ? Constants.Blocks.CommandNoValueHeight : Constants.Blocks.DefaultHeight;

            GameObject go = NewRect(entry.label, w, h);
            BodyParts parts = AddBlockBody(go, GetColor(entry.category), sprite);
            if (go.TryGetComponent<RectTransform>(out RectTransform goRt))
                goRt.sizeDelta = new Vector2(w, h);

            go.AddComponent<CanvasGroup>();
            if (draggable)
            {
                AddDraggable(go, entry, ctx, parts);
            }

            await AddLabel(go, entry.label, (int)Constants.Blocks.LabelFontSize);

            // Logic 블록은 수평 체인 슬롯 포함
            if (entry.category == BlockCategory.Logic && entry.chainBlocks is not null)
                await AppendChain(go, entry.chainBlocks, ctx, draggable);

            return go;
        }

        // ── FlowControl 블록 (C자형) ────────────────────────────
        // 만약/반복하기는 같은 카테고리라 라벨로 아트를 구분한다 (각각 전용 프리팹).
        // 분류 기준(라벨 문자열 비교)은 여기 한 곳에만 있고, 생성 시점(FlowPrefabName)과
        // 부착 시점(AttachSockets)이 같은 함수를 호출해 서로 다른 결과를 낼 수 없게 한다.
        private enum FlowKind { Other, While, If }

        /// <summary>
        /// 라벨로 FlowControl 블록 종류(반복하기/만약/기타)를 판별한다.
        /// </summary>
        private static FlowKind GetFlowKind(string label) =>
            label == Constants.BlockLabels.While ? FlowKind.While :
            label == Constants.BlockLabels.If    ? FlowKind.If    : FlowKind.Other;

        /// <summary>
        /// FlowControl 블록 종류에 맞는 프리팹 이름을 반환한다.
        /// </summary>
        private static string FlowPrefabName(string label) => GetFlowKind(label) switch
        {
            FlowKind.While => Constants.BlockAssets.WhilePrefab,
            FlowKind.If    => Constants.BlockAssets.IfPrefab,
            _              => Constants.BlockAssets.FlowControlPrefab
        };

        /// <summary>
        /// C자형 FlowControl 블록을 프리팹으로 만들고, else 분기가 있으면 헤더와 Inner 컨테이너를 덧붙인다.
        /// </summary>
        private static async UniTask<GameObject> CreateFlowBlock(BlockEntry entry, BuildContext ctx, bool draggable)
        {
            // 기본 구조(라벨·헤더·Inner·푸터)는 프리팹이 담당
            GameObject go = await CreateFromPrefab(FlowPrefabName(entry.label), entry, ctx, draggable);

            // 사전 배치 블록은 현재 미지원 (런타임 드래그로만 배치)
            if (entry.innerBlocks is not null && entry.innerBlocks.Length > 0 && ctx.Logger != null)
                ctx.Logger.ZLogWarning($"[BlockFactory] 사전 배치 innerBlocks는 아직 지원되지 않습니다.");

            // else 분기 (만약 블록) — 프리팹 기본 구조 뒤에 런타임 추가 후 푸터를 맨 아래로
            if (entry.elseBlocks is not null && entry.elseBlocks.Length > 0)
            {
                await AppendFlowHeader(go.transform, Constants.BlockLabels.Else, Constants.Blocks.FlowElseHeight);
                AppendInnerContainer(go.transform, entry.elseBlocks, ctx);

                Transform footer = go.TryGetComponent(out CodingBlock block) ? block.Footer : null;
                if (footer)
                    footer.SetAsLastSibling();
                else if (ctx.Logger != null)
                    ctx.Logger.ZLogWarning($"[BlockFactory] {go.name} 블록의 CodingBlock에 Footer가 연결되지 않았습니다.");
            }

            return go;
        }

        // ── FlowControl 헬퍼 ────────────────────────────────────

        /// <summary>
        /// else 구분 헤더(스페이서 + 텍스트)를 덧붙인다 (시각은 Label GO의 9-slice 배경이 담당).
        /// </summary>
        private static async UniTask AppendFlowHeader(Transform parent, string label, float height)
        {
            GameObject h = NewRect(Constants.BlockParts.HeaderPrefix + label, 0f, height);
            h.transform.SetParent(parent, false);
            await AddLabel(h, label, (int)Constants.Blocks.LabelFontSize);
            LayoutElement le = h.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
        }

        private const float InnerMinHeight = Constants.Blocks.FlowInnerMinHeight;

        /// <summary>
        /// 진입 소켓·빈 상태 표시·하단 기준점을 갖춘 Inner 컨테이너를 코드로 만들어 블록에 덧붙인다.
        /// </summary>
        private static void AppendInnerContainer(Transform parent, BlockEntry[] blocks, BuildContext ctx)
        {
            GameObject inner = NewRect(Constants.BlockParts.InnerPrefix, 0f, InnerMinHeight);
            inner.transform.SetParent(parent, false);

            LayoutElement le = inner.AddComponent<LayoutElement>();
            le.preferredHeight = InnerMinHeight;
            le.minHeight = InnerMinHeight;
            le.flexibleWidth = 1f;

            // 진입 소켓: 내부 영역 상단 중앙
            GameObject socketGo = new GameObject(Constants.Sockets.InnerName, typeof(RectTransform));
            socketGo.transform.SetParent(inner.transform, false);
            socketGo.TryGetComponent(out RectTransform socketRt);
            socketRt.anchorMin = socketRt.anchorMax = new Vector2(0.5f, 1f);
            socketRt.pivot = new Vector2(0.5f, 0.5f);
            socketRt.sizeDelta = Vector2.zero;
            socketRt.anchoredPosition = Constants.Sockets.FlowInner;
            InnerSocket innerSocket = socketGo.AddComponent<InnerSocket>();
            if (parent.TryGetComponent(out CodingBlock owner))
                owner.RegisterSocket(innerSocket);
            else if (ctx.Logger != null)
                ctx.Logger.ZLogWarning($"[BlockFactory] {parent.name}에 CodingBlock이 없어 Inner 소켓의 소유 블록을 지정하지 못했습니다.");

            // 빈 상태 표시 (블록이 들어오면 숨겨짐)
            GameObject empty = NewRect(Constants.BlockParts.EmptyIndicator, 0f, EmptySlotHeight);
            empty.transform.SetParent(inner.transform, false);
            empty.TryGetComponent<RectTransform>(out RectTransform emptyRt);
            emptyRt.anchorMin = new Vector2(0.14f, 0.5f);
            emptyRt.anchorMax = new Vector2(0.97f, 0.5f);
            emptyRt.pivot = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta = new Vector2(0f, EmptySlotHeight);
            emptyRt.anchoredPosition = Vector2.zero;
            Image emptyImg = empty.AddComponent<Image>();
            emptyImg.color = Constants.HighlightColors.EmptySlot;
            innerSocket.SetEmptyIndicator(empty);

            // 체인 하단 기준점: Inner 바닥 중앙 — FlowInnerResize가 마지막 ChainOutSocket과 이 위치를 맞춰 높이를 계산
            GameObject bottomSocketGo = new GameObject(Constants.Sockets.InnerBottomName, typeof(RectTransform));
            bottomSocketGo.transform.SetParent(inner.transform, false);
            bottomSocketGo.TryGetComponent(out RectTransform bottomRt);
            bottomRt.anchorMin = bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0.5f);
            bottomRt.sizeDelta = Vector2.zero;
            bottomRt.anchoredPosition = Constants.Sockets.FlowInnerBottom;
            bottomSocketGo.AddComponent<InnerBottomSocket>();

            // 사전 배치 블록은 현재 미지원 (런타임 드래그로만 배치)
            if (blocks is not null && blocks.Length > 0 && ctx.Logger != null)
                ctx.Logger.ZLogWarning($"[BlockFactory] 사전 배치 innerBlocks는 아직 지원되지 않습니다.");

            FlowInnerResize resize = inner.AddComponent<FlowInnerResize>();
            resize.SetSockets(innerSocket, bottomRt);

            // 프리팹 인스턴스화 이후에 덧붙인 컴포넌트라 여기서 따로 주입한다
            ctx.Resolver.InjectGameObject(inner);
        }

        // ── Logic 블록 (그리고 / 또는) ───────────────────────────────────

        /// <summary>
        /// ConditionIn/Out 소켓으로 조건 체인에 연결되는 단순 레이블 Logic 블록을 코드로 조립한다.
        /// </summary>
        private static async UniTask<GameObject> CreateLogicBlock(BlockEntry entry, BuildContext ctx, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(BlockCategory.Logic);
            GameObject go = NewRect(entry.label, 120f, 56f);
            BodyParts parts = AddBlockBody(go, GetColor(BlockCategory.Logic), sprite);

            // 스프라이트 원본 크기 기준 확대 — transform 스케일 대신 크기·라벨을 함께 키움
            if (sprite && go.TryGetComponent<RectTransform>(out RectTransform rt))
                rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * Constants.Blocks.LogicScale;

            go.AddComponent<CanvasGroup>();
            if (draggable)
                AddDraggable(go, entry, ctx, parts);
            await AddLabel(go, entry.label, (int)Constants.Blocks.LabelFontSize);
            return go;
        }

        // ── Logic 체인 (수평) ───────────────────────────────────

        /// <summary>
        /// 기준 블록을 수평 레이아웃 컨테이너로 감싸고 체인 블록들을 옆으로 이어 붙인다.
        /// </summary>
        private static async UniTask AppendChain(GameObject baseBlock, BlockEntry[] chain, BuildContext ctx, bool draggable)
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

            foreach (BlockEntry c in chain)
            {
                GameObject child = await Create(c, ctx, draggable);
                child.transform.SetParent(container.transform, false);
            }
        }

        /// <summary>
        /// 블록 카테고리에 따른 연결부 소켓을 생성·배치하고 블록 소유로 등록한다 (이미 있으면 건너뜀).
        /// </summary>
        public static void AttachSockets(CodingBlock block)
        {
            BlockCategory cat = block.Category;

            // Command만 단일 ValueOutSocket — FlowControl은 헤더에 내장, Logic은 두 조건 슬롯 내장
            // ValueKind.None인 Command는 값 슬롯 없는 블록이므로 소켓을 붙이지 않는다 (Value 스냅 불가)
            if (cat == BlockCategory.Command && block.ValueKind != ValueKind.None)
                AttachValueOutSocket(block, Constants.Sockets.CommandValueOut);

            // Logic은 전용 아트(Logic)라 좌측 노치 오프셋이 Condition과 다르다
            if (cat == BlockCategory.Value)
                AttachValueInSocket(block, Constants.Sockets.ValueValueIn);
            else if (cat == BlockCategory.Logic)
                AttachValueInSocket(block, Constants.Sockets.LogicConditionIn);
            else if (cat == BlockCategory.Condition)
                AttachValueInSocket(block, Constants.Sockets.ConditionValueIn);

            // Condition / Logic 블록: 수평 조건 체인 소켓
            if (cat == BlockCategory.Logic)
            {
                AttachConditionInSocket(block,  Constants.Sockets.LogicConditionIn);
                AttachConditionOutSocket(block, Constants.Sockets.LogicConditionOut);
            }
            else if (cat == BlockCategory.Condition)
            {
                AttachConditionInSocket(block,  Constants.Sockets.ConditionIn);
                AttachConditionOutSocket(block, Constants.Sockets.ConditionOut);
            }

            // FunctionDef(함수 정의)는 체인에 연결되지 않는 독립 컨테이너 — 체인 소켓을 붙이지 않는다
            if (cat != BlockCategory.Value && cat != BlockCategory.Logic
                && cat != BlockCategory.Condition && cat != BlockCategory.FunctionDef)
            {
                bool isStart   = block.ControlRole == ControlRole.Start;
                bool isEnd     = block.ControlRole == ControlRole.End;
                bool isCommand = cat == BlockCategory.Command;
                bool isFlow    = cat == BlockCategory.FlowControl;
                // 값 슬롯 없는 Command는 폭이 좁아 체인 소켓 오프셋을 별도 사용.
                // 함수 블록은 CommandNoValue와 동일한 크기이므로 같은 오프셋을 공유한다.
                bool isNoValueCommand = isCommand && block.ValueKind == ValueKind.None;
                bool useNoValueOffset = isNoValueCommand || cat == BlockCategory.Function || cat == BlockCategory.Else;

                // 만약/반복하기는 전용 아트라 체인 소켓 오프셋도 각각 별도.
                // 분류는 GetFlowKind 하나만 사용 — FlowPrefabName(생성 시점)과 같은 기준을 공유한다
                FlowKind flowKind = isFlow ? GetFlowKind(block.name) : FlowKind.Other;
                bool isWhile = flowKind == FlowKind.While;
                bool isIf    = flowKind == FlowKind.If;

                Vector2 outOffset = isStart          ? Constants.Sockets.StartChainOut          :
                                    isWhile          ? Constants.Sockets.WhileChainOut          :
                                    isIf             ? Constants.Sockets.IfChainOut             :
                                    isFlow           ? Constants.Sockets.FlowChainOut           :
                                    useNoValueOffset ? Constants.Sockets.CommandNoValueChainOut :
                                    isCommand        ? Constants.Sockets.CommandChainOut        : Vector2.zero;
                Vector2 inOffset  = isEnd            ? Constants.Sockets.EndChainIn             :
                                    isWhile          ? Constants.Sockets.WhileChainIn           :
                                    isIf             ? Constants.Sockets.IfChainIn              :
                                    isFlow           ? Constants.Sockets.FlowChainIn            :
                                    useNoValueOffset ? Constants.Sockets.CommandNoValueChainIn  :
                                    isCommand        ? Constants.Sockets.CommandChainIn         : Vector2.zero;

                if (!isEnd)   AttachOutSocket(block, outOffset);
                if (!isStart) AttachInSocket(block,  inOffset);
            }
        }

        /// <summary>
        /// 블록 직속으로 상수 이름을 붙인 소켓 오브젝트를 만들고 앵커·오프셋을 지정한 뒤 소켓 컴포넌트를 붙인다.
        /// </summary>
        private static T CreateSocketObject<T>(CodingBlock block, string socketName, Vector2 anchor, Vector2 offset) where T : Component
        {
            GameObject go = new GameObject(socketName, typeof(RectTransform));
            go.transform.SetParent(block.transform, false);
            go.TryGetComponent(out RectTransform rt);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return go.AddComponent<T>();
        }

        /// <summary>
        /// 블록 하단 중앙(앵커 0.5, 0)에 ChainOutSocket을 후부착한다.
        /// </summary>
        public static void AttachOutSocket(CodingBlock block, Vector2 offset = default)
        {
            if (block.GetSocket<ChainOutSocket>()) return;
            block.RegisterSocket(CreateSocketObject<ChainOutSocket>(block, Constants.Sockets.ChainOutName, new Vector2(0.5f, 0f), offset));
        }

        /// <summary>
        /// 블록 상단 중앙(앵커 0.5, 1)에 ChainInSocket을 후부착한다.
        /// </summary>
        public static void AttachInSocket(CodingBlock block, Vector2 offset = default)
        {
            if (BlockSocket.FindChildComponent<ChainInSocket>(block.transform, Constants.Sockets.ChainInName)) return;
            CreateSocketObject<ChainInSocket>(block, Constants.Sockets.ChainInName, new Vector2(0.5f, 1f), offset);
        }

        /// <summary>
        /// Command 블록 우측 중앙(앵커 1, 0.5)에 ValueOutSocket을 후부착한다.
        /// </summary>
        public static void AttachValueOutSocket(CodingBlock commandBlock, Vector2 offset = default)
        {
            if (commandBlock.GetSocket<ValueOutSocket>()) return;
            commandBlock.RegisterSocket(CreateSocketObject<ValueOutSocket>(commandBlock, Constants.Sockets.ValueOutName, new Vector2(1f, 0.5f), offset));
        }

        /// <summary>
        /// Value 블록 좌측 중앙(앵커 0, 0.5)에 ValueInSocket을 후부착한다.
        /// </summary>
        public static void AttachValueInSocket(CodingBlock valueBlock, Vector2 offset = default)
        {
            if (BlockSocket.FindChildComponent<ValueInSocket>(valueBlock.transform, Constants.Sockets.ValueInName)) return;
            CreateSocketObject<ValueInSocket>(valueBlock, Constants.Sockets.ValueInName, new Vector2(0f, 0.5f), offset);
        }

        // ── Condition 체인 소켓 ─────────────────────────────────

        /// <summary>
        /// 조건/Logic 블록 우측 중앙(앵커 1, 0.5)에 ConditionOutSocket을 후부착한다.
        /// </summary>
        public static void AttachConditionOutSocket(CodingBlock block, Vector2 offset = default)
        {
            if (block.GetSocket<ConditionOutSocket>()) return;
            block.RegisterSocket(CreateSocketObject<ConditionOutSocket>(block, Constants.Sockets.ConditionOutName, new Vector2(1f, 0.5f), offset));
        }

        /// <summary>
        /// 조건/Logic 블록 좌측 중앙(앵커 0, 0.5)에 ConditionInSocket을 후부착한다.
        /// </summary>
        public static void AttachConditionInSocket(CodingBlock block, Vector2 offset = default)
        {
            if (BlockSocket.FindChildComponent<ConditionInSocket>(block.transform, Constants.Sockets.ConditionInName)) return;
            CreateSocketObject<ConditionInSocket>(block, Constants.Sockets.ConditionInName, new Vector2(0f, 0.5f), offset);
        }

        // 비어있는 Inner 슬롯 표시(EmptyIndicator) 높이
        private const float EmptySlotHeight = 60f;

        // ── 아웃라인 오버레이 머티리얼 3종 ──────────────────────────
        // Full: 전체 범위 (에러 표시)
        // Bottom: 하단 35% (체인 스냅 표시)
        // Right: 우측 20% (값 스냅 표시)
        private static Material _spriteFillMaterial;
        private static Material _spriteFillMaterialBottom;
        private static Material _spriteFillMaterialInner;
        private static Material _spriteFillMaterialRight;

        public static Material SpriteFillMaterial       => GetOrLoadMaterial(ref _spriteFillMaterial,       "BlockOutlineFull",   0.0f, 1.0f, 0.0f, 1.0f);
        public static Material SpriteFillMaterialBottom => GetOrLoadMaterial(ref _spriteFillMaterialBottom, "BlockOutlineBottom", 0.0f, Constants.HighlightSettings.ChainHighlightYMax, 0.0f, 1.0f);
        public static Material SpriteFillMaterialInner  => GetOrLoadMaterial(ref _spriteFillMaterialInner,  "BlockOutlineInner",  0.0f, Constants.HighlightSettings.InnerHighlightYMax, 0.0f, 1.0f);
        public static Material SpriteFillMaterialRight  => GetOrLoadMaterial(ref _spriteFillMaterialRight,  "BlockOutlineRight",  0.0f, 1.0f, Constants.HighlightSettings.ValueHighlightXMin, 1.0f);

        /// <summary>
        /// 캐시된 영역 제한 머티리얼을 반환하고, 없으면 새로 만든다.
        /// </summary>
        private static Material GetOrLoadMaterial(ref Material cache, string matName, float yMin, float yMax, float xMin, float xMax)
        {
            if (!cache) cache = MakeSpriteFillMat(matName, yMin, yMax, xMin, xMax);
            return cache;
        }

        /// <summary>
        /// SpriteFill 셰이더로 UV 영역을 제한한 하이라이트 머티리얼을 만든다.
        /// </summary>
        private static Material MakeSpriteFillMat(string matName, float yMin, float yMax, float xMin, float xMax)
        {
            Shader shader = Shader.Find(Constants.ResourcePaths.SpriteFillShader);
            if (!shader)
            {
                // 정적 머티리얼 프로퍼티 경로라 로거를 받을 수 없어 Debug로 대체 출력한다
                Debug.LogWarning($"[BlockFactory] '{Constants.ResourcePaths.SpriteFillShader}' 셰이더를 찾을 수 없습니다.");
                return null;
            }

            Material mat = new Material(shader) { name = matName };
            mat.SetFloat("_YMin", yMin);
            mat.SetFloat("_YMax", yMax);
            mat.SetFloat("_XMin", xMin);
            mat.SetFloat("_XMax", xMax);
            return mat;
        }

        // ── 공통 유틸 ───────────────────────────────────────────

        /// <summary>
        /// 지정 크기의 RectTransform만 가진 빈 UI 오브젝트를 만든다.
        /// </summary>
        private static GameObject NewRect(string name, float w, float h)
        {
            GameObject go = new GameObject(name);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        /// <summary>
        /// 내부 구조용 이미지(InnerContainer, EmptySlot 등)를 go에 직접 붙인다.
        /// </summary>
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

        // 코드로 조립한 블록 본체의 파츠 — CodingBlock.SetParts로 넘긴다
        private readonly struct BodyParts
        {
            public readonly Image Outline;
            public readonly Image ChainHighlight;
            public readonly Image ValueHighlight;
            public readonly Image Body;

            /// <summary>
            /// 파츠 묶음을 만든다.
            /// </summary>
            public BodyParts(Image outline, Image chainHighlight, Image valueHighlight, Image body)
            {
                Outline = outline;
                ChainHighlight = chainHighlight;
                ValueHighlight = valueHighlight;
                Body = body;
            }
        }

        /// <summary>
        /// 블록 본체용 시각 계층을 구성하고 만든 파츠를 반환한다.
        /// 렌더링 순서 — sibling 0~2(하이라이트) → sibling 3(주 이미지) → sibling 4+(레이블·소켓).
        /// </summary>
        private static BodyParts AddBlockBody(GameObject go, Color color, Sprite sprite)
        {
            if (sprite && go.TryGetComponent<RectTransform>(out RectTransform goRt))
                goRt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);

            if (sprite)
            {
                (Image outline, Image chain, Image value) = AddHighlightOverlays(go, sprite);

                GameObject spriteGo = new GameObject(Constants.BlockParts.Sprite, typeof(RectTransform));
                spriteGo.transform.SetParent(go.transform, false);
                spriteGo.TryGetComponent(out RectTransform srt);
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = srt.offsetMax = Vector2.zero;
                Image spriteImg = spriteGo.AddComponent<Image>();
                spriteImg.sprite = sprite;
                spriteImg.type = Image.Type.Simple;
                spriteImg.preserveAspect = false;
                spriteImg.color = Color.white;
                return new BodyParts(outline, chain, value, spriteImg);
            }

            Image border = AddBorderOverlay(go);

            GameObject fillGo = new GameObject(Constants.BlockParts.Fill, typeof(RectTransform));
            fillGo.transform.SetParent(go.transform, false);
            fillGo.TryGetComponent(out RectTransform frt);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = color;
            return new BodyParts(border, null, null, fillImg);
        }

        /// <summary>
        /// 스프라이트 블록용 방향별 하이라이트 오버레이 3종(기본 투명, 런타임에 색 변경)을 만든다.
        /// sibling 0: SpriteOutline(전체, 컴파일 에러) / 1: ChainHighlight(하단, 체인 스냅) / 2: ValueHighlight(우측, 값 스냅).
        /// </summary>
        private static (Image outline, Image chain, Image value) AddHighlightOverlays(GameObject blockGo, Sprite sprite)
        {
            float t = Constants.HighlightSettings.OutlineThickness;
            Vector2 minOffFull = new Vector2(-t, -t);
            Vector2 maxOffFull = new Vector2(t, t);

            Vector2 minOffChain = new Vector2(0f, -t);
            Vector2 maxOffChain = new Vector2(0f, t);

            Image outline = AddOverlay(Constants.BlockParts.Outline, blockGo, sprite, Vector2.zero, Vector2.one, minOffFull, maxOffFull, SpriteFillMaterial);
            Image chain = AddOverlay(Constants.BlockParts.ChainHighlight, blockGo, sprite, Vector2.zero, Vector2.one, minOffChain, maxOffChain,
                SpriteFillMaterialBottom);
            Image value = AddOverlay(Constants.BlockParts.ValueHighlight, blockGo, sprite, Vector2.zero, Vector2.one, minOffFull, maxOffFull,
                SpriteFillMaterialRight);
            return (outline, chain, value);
        }

        /// <summary>
        /// 투명한 하이라이트 오버레이 이미지 하나를 만들어 반환한다.
        /// </summary>
        private static Image AddOverlay(string name, GameObject parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Material mat = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            go.TryGetComponent(out RectTransform rt);
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
            Material useMat = mat ? mat : SpriteFillMaterial;
            if (useMat) img.material = useMat;
            return img;
        }

        /// <summary>
        /// 스프라이트 없는 블록 본체용 테두리(기본 투명, 런타임에 색 변경)를 만들어 반환한다.
        /// </summary>
        private static Image AddBorderOverlay(GameObject go, float thickness = 2f)
        {
            GameObject border = new GameObject(Constants.BlockParts.Outline, typeof(RectTransform));
            border.transform.SetParent(go.transform, false);
            border.transform.SetAsFirstSibling();
            border.TryGetComponent(out RectTransform rt);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-thickness, -thickness);
            rt.offsetMax = new Vector2(thickness, thickness);
            Image img = border.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// 블록 전체를 덮는 중앙 정렬 라벨 TMP를 만든다.
        /// </summary>
        private static async UniTask AddLabel(GameObject go, string text, int size = 28, float bottom = 0f)
        {
            TMPro.TMP_FontAsset font = await LoadLabelFontAsync();
            GameObject t = new GameObject(Constants.BlockParts.Label, typeof(RectTransform));
            t.SetActive(false);
            t.transform.SetParent(go.transform, false);
            t.TryGetComponent(out RectTransform rt);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, bottom);
            rt.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI txt = t.AddComponent<TMPro.TextMeshProUGUI>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = size;
            txt.color = Color.white;
            txt.alignment = TMPro.TextAlignmentOptions.Center;
            t.SetActive(true);
        }

        /// <summary>
        /// 코드로 조립한 블록에 CodingBlock을 붙여 메타·파츠를 설정하고 의존성을 주입한다.
        /// </summary>
        private static void AddDraggable(GameObject go, BlockEntry entry, BuildContext ctx, BodyParts parts)
        {
            if (go.TryGetComponent<RectTransform>(out RectTransform brt))
                brt.pivot = new Vector2(0f, entry.category == BlockCategory.FlowControl ? 1f : 0.5f);
            CodingBlock block = go.AddComponent<CodingBlock>();
            block.Init(entry.category, ctx.RootCanvas, entry.valueKind, entry.controlRole);
            block.SetParts(parts.Outline, parts.ChainHighlight, parts.ValueHighlight, parts.Body);
            ctx.Resolver.Inject(block);
        }
    }
}
