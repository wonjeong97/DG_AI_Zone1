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
        /// 코드로 조립하는 블록(동작·조건 동작·논리·아니면)의 스프라이트를 로드한다 (캐시 우선).
        /// 나머지 카테고리는 프리팹이 아트를 갖고 있어 여기를 거치지 않는다.
        /// </summary>
        private static async UniTask<Sprite> LoadSpriteAsync(BlockCategory cat)
        {
            string name = cat switch
            {
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
        public static async UniTask<GameObject> Create(BlockEntry entry, BuildContext ctx)
        {
            GameObject go = entry.category switch
            {
                BlockCategory.Value       => await CreateFromPrefab(Constants.BlockAssets.ValuePrefab, entry, ctx),
                // 조건 블록 — None: 이벤트형 조건(전용 아트) / kind 지정: 값형 조건(Value 아트 재사용)
                BlockCategory.Condition   => await CreateFromPrefab(entry.valueKind == ValueKind.None
                    ? Constants.BlockAssets.ConditionPrefab
                    : Constants.BlockAssets.ValuePrefab, entry, ctx),
                // Command + ValueKind.None = 값 슬롯 없는 동작 블록 (전용 아트, ValueOutSocket 미부착)
                BlockCategory.Command     => await CreateFromPrefab(entry.valueKind == ValueKind.None
                    ? Constants.BlockAssets.CommandNoValuePrefab
                    : Constants.BlockAssets.CommandPrefab, entry, ctx),
                BlockCategory.Control     => await CreateFromPrefab(entry.controlRole == ControlRole.Start
                    ? Constants.BlockAssets.StartPrefab
                    : Constants.BlockAssets.EndPrefab, entry, ctx),
                // 함수 블록 — CommandNoValue와 동일한 구조(값 슬롯 없음, 체인 소켓만)
                BlockCategory.Function    => await CreateFromPrefab(Constants.BlockAssets.FunctionPrefab, entry, ctx),
                // 함수 구현 블록 — FlowControl과 동일한 C자 컨테이너(헤더 값 슬롯 없음)
                BlockCategory.FunctionDef => await CreateFromPrefab(Constants.BlockAssets.FuncDefPrefab, entry, ctx),
                BlockCategory.FlowControl => await CreateFlowBlock(entry, ctx),
                BlockCategory.Logic       => await CreateLogicBlock(entry, ctx),
                _                         => await CreateSimpleBlock(entry, ctx)
            };

            // 인벤토리에서 최초 드래그 시에도 정확한 소켓 위치(스냅 기준점)를 기준으로 탐색되도록 소켓을 미리 부착
            if (go && go.TryGetComponent<CodingBlock>(out CodingBlock block))
                AttachSockets(block);
            else if (ctx.Logger != null)
                ctx.Logger.ZLogWarning($"[BlockFactory] '{entry.label}' 블록에 CodingBlock이 없어 소켓을 붙이지 못했습니다.");

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
        private static async UniTask<GameObject> CreateFromPrefab(string prefabName, BlockEntry entry, BuildContext ctx)
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

            block.Init(entry.category, ctx.RootCanvas, entry.valueKind, entry.controlRole);
            return go;
        }

        // ── 단순 블록 ───────────────────────────────────────────

        /// <summary>
        /// 스프라이트 본체와 라벨만 있는 블록을 코드로 조립한다.
        /// </summary>
        private static async UniTask<GameObject> CreateSimpleBlock(BlockEntry entry, BuildContext ctx)
        {
            Sprite sprite = await LoadSpriteAsync(entry.category);

            // 아니면 — CommandNoValue와 동일한 크기(값 슬롯 없는 체인 블록)
            bool isElse = entry.category == BlockCategory.Else;
            float w = isElse ? Constants.Blocks.CommandNoValueWidth  : Constants.Blocks.DefaultWidth;
            float h = isElse ? Constants.Blocks.CommandNoValueHeight : Constants.Blocks.DefaultHeight;

            GameObject go = NewRect(entry.label, w, h);
            BodyParts parts = AddBlockBody(go, GetColor(entry.category), sprite);
            if (go.TryGetComponent<RectTransform>(out RectTransform goRt))
                goRt.sizeDelta = new Vector2(w, h);

            go.AddComponent<CanvasGroup>();
            AddDraggable(go, entry, ctx, parts);
            await AddLabel(go, entry.label);
            return go;
        }

        // ── FlowControl 블록 (C자형) ────────────────────────────
        // 만약/반복하기는 같은 카테고리라 라벨로 아트를 구분한다 (각각 전용 프리팹).
        // 분류 기준(라벨 문자열 비교)은 여기 한 곳에만 있고, 생성 시점(FlowPrefabName)과
        // 부착 시점(AttachSockets)이 같은 함수를 호출해 서로 다른 결과를 낼 수 없게 한다.
        private enum FlowKind { Other, While, If }

        /// <summary>
        /// 라벨로 FlowControl 블록 종류(반복하기/만약/기타)를 판별한다.
        /// 반복하기는 '반복하기(무한)'처럼 표기가 붙으므로 키워드 포함으로 본다 (CodingBlock.IsRepeat와 같은 기준).
        /// </summary>
        private static FlowKind GetFlowKind(string label) =>
            label.Contains(Constants.BlockLabels.RepeatKeyword) ? FlowKind.While :
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
        private static async UniTask<GameObject> CreateFlowBlock(BlockEntry entry, BuildContext ctx)
        {
            // 기본 구조(라벨·헤더·Inner·푸터)는 프리팹이 담당
            GameObject go = await CreateFromPrefab(FlowPrefabName(entry.label), entry, ctx);

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
            await AddLabel(h, label, true); // ㄷ자 블록 배경은 레이캐스트를 받지 않아 헤더 라벨이 드래그 영역이다
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
            emptyImg.raycastTarget = false; // 표시 전용 — 드래그·드롭은 블록 본체가 받는다
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
        private static async UniTask<GameObject> CreateLogicBlock(BlockEntry entry, BuildContext ctx)
        {
            Sprite sprite = await LoadSpriteAsync(BlockCategory.Logic);
            GameObject go = NewRect(entry.label, LogicFallbackWidth, Constants.Blocks.DefaultHeight);
            BodyParts parts = AddBlockBody(go, GetColor(BlockCategory.Logic), sprite);

            // 스프라이트 원본 크기 기준 확대 — transform 스케일 대신 크기·라벨을 함께 키움
            if (sprite && go.TryGetComponent<RectTransform>(out RectTransform rt))
                rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * Constants.Blocks.LogicScale;

            go.AddComponent<CanvasGroup>();
            AddDraggable(go, entry, ctx, parts);
            await AddLabel(go, entry.label);
            return go;
        }

        // Logic 스프라이트를 못 불러왔을 때의 블록 폭 (스프라이트가 있으면 원본 크기를 쓴다)
        private const float LogicFallbackWidth = 120f;

        /// <summary>
        /// 블록 카테고리에 따른 연결부 소켓을 생성·배치하고 블록 소유로 등록한다 (이미 붙였으면 건너뜀).
        /// 생성 직후 한 번 붙인 뒤에도 드래그 시작·스냅·드롭마다 다시 불리므로, 소켓을 일일이 찾지 않도록 블록에 표시해 둔다.
        /// </summary>
        public static void AttachSockets(CodingBlock block)
        {
            if (block.SocketsAttached) return;

            BlockCategory cat = block.Category;

            // Command만 단일 ValueOutSocket — 만약의 조건 슬롯은 프리팹 헤더에 내장, Logic은 조건 체인 소켓(ConditionIn/Out)으로 잇는다
            // ValueKind.None인 Command는 값 슬롯 없는 블록이므로 소켓을 붙이지 않는다 (Value 스냅 불가)
            if (cat == BlockCategory.Command && block.ValueKind != ValueKind.None)
                AttachValueOutSocket(block, Constants.Sockets.CommandValueOut);

            // 값 슬롯에 끼우는 블록의 좌측 연결부 — Logic은 값 슬롯에 붙지 않으므로 붙이지 않는다
            if (cat == BlockCategory.Value)
                AttachValueInSocket(block, Constants.Sockets.ValueValueIn);
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

            block.MarkSocketsAttached();
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

        // ── 외곽선·스냅 하이라이트 머티리얼 ─────────────────────────
        // 모두 Custom/UI/BlockOutline으로 블록 모양 둘레에 일정 두께 테두리를 그리고(BlockOutlineMesh와 함께) 보일 영역만 다르다.
        // Full: 전체 (컴파일 에러·성공) / Bottom: 아래 체인 연결부 / Right: 오른쪽 값 칸 / Inner: ㄷ자 안쪽 진입부(스프라이트별)
        private static Material _outlineMaterial;
        private static Material _outlineMaterialBottom;
        private static Material _outlineMaterialRight;
        private readonly static Dictionary<Sprite, Material> _innerOutlineMaterials = new();

        public static Material OutlineMaterial       => GetOrCreateOutlineMaterial(ref _outlineMaterial,       "BlockOutline",       0f, 1f, 0f, 1f);
        public static Material OutlineMaterialBottom => GetOrCreateOutlineMaterial(ref _outlineMaterialBottom, "BlockOutlineBottom", 0f, Constants.HighlightSettings.ChainHighlightYMax, 0f, 1f);
        public static Material OutlineMaterialRight  => GetOrCreateOutlineMaterial(ref _outlineMaterialRight,  "BlockOutlineRight",
            Constants.HighlightSettings.ValueHighlightYMin, Constants.HighlightSettings.ValueHighlightYMax, Constants.HighlightSettings.ValueHighlightXMin, 1f);

        /// <summary>
        /// ㄷ자 블록 안쪽 진입 하이라이트 머티리얼을 스프라이트별로 반환한다 (없으면 만든다).
        /// 머리 아래 가장자리 아래만 보이고, 머리와 머리 아래 돌기만 부풀린다(왼쪽 팔 제외) — 픽셀 기준 값을 이 스프라이트의 비율로 바꾼다.
        /// </summary>
        public static Material GetInnerOutlineMaterial(Sprite sprite)
        {
            if (!sprite) return null;
            if (_innerOutlineMaterials.TryGetValue(sprite, out Material cached) && cached) return cached;

            float w = sprite.rect.width;
            float h = sprite.rect.height;
            Material mat = MakeOutlineMat("BlockOutlineInner", 0f, (h - Constants.HighlightSettings.InnerHighlightHeaderBottomPx) / h, 0f, 1f);
            if (mat)
            {
                mat.SetFloat(OutlineSrcXMinId, Constants.HighlightSettings.InnerHighlightSourceLeftPx / w);
                mat.SetFloat(OutlineSrcYMinId, (h - Constants.HighlightSettings.InnerHighlightSourceBottomPx) / h);
            }
            _innerOutlineMaterials[sprite] = mat;
            return mat;
        }

        /// <summary>
        /// 캐시된 하이라이트 머티리얼을 반환하고, 없으면 새로 만든다.
        /// </summary>
        private static Material GetOrCreateOutlineMaterial(ref Material cache, string matName, float yMin, float yMax, float xMin, float xMax)
        {
            if (!cache) cache = MakeOutlineMat(matName, yMin, yMax, xMin, xMax);
            return cache;
        }

        /// <summary>
        /// BlockOutline 셰이더로 보일 영역(스프라이트 기준 0~1, 0·1이면 그쪽 바깥 테두리까지)을 정한 하이라이트 머티리얼을 만든다.
        /// </summary>
        private static Material MakeOutlineMat(string matName, float yMin, float yMax, float xMin, float xMax)
        {
            Shader shader = Shader.Find(Constants.ResourcePaths.BlockOutlineShader);
            if (!shader)
            {
                // 정적 머티리얼 프로퍼티 경로라 로거를 받을 수 없어 Debug로 대체 출력한다
                Debug.LogWarning($"[BlockFactory] '{Constants.ResourcePaths.BlockOutlineShader}' 셰이더를 찾을 수 없습니다.");
                return null;
            }

            Material mat = new Material(shader) { name = matName };
            mat.SetFloat(OutlineYMinId, yMin);
            mat.SetFloat(OutlineYMaxId, yMax);
            mat.SetFloat(OutlineXMinId, xMin);
            mat.SetFloat(OutlineXMaxId, xMax);
            return mat;
        }

        // BlockOutline 셰이더 프로퍼티 — 하이라이트가 보일 영역과 ㄷ자 안쪽 하이라이트의 원본 영역
        private readonly static int OutlineYMinId = Shader.PropertyToID("_YMin");
        private readonly static int OutlineYMaxId = Shader.PropertyToID("_YMax");
        private readonly static int OutlineXMinId = Shader.PropertyToID("_XMin");
        private readonly static int OutlineXMaxId = Shader.PropertyToID("_XMax");
        private readonly static int OutlineSrcXMinId = Shader.PropertyToID("_SrcXMin");
        private readonly static int OutlineSrcYMinId = Shader.PropertyToID("_SrcYMin");

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
        /// 스프라이트 블록용 하이라이트 오버레이 3종(기본 투명, 런타임에 색 변경)을 만든다.
        /// sibling 0: SpriteOutline(전체, 컴파일 결과) / 1: ChainHighlight(아래, 체인 스냅) / 2: ValueHighlight(오른쪽, 값 스냅).
        /// </summary>
        private static (Image outline, Image chain, Image value) AddHighlightOverlays(GameObject blockGo, Sprite sprite)
        {
            Image outline = AddOverlay(Constants.BlockParts.Outline, blockGo, sprite, OutlineMaterial);
            Image chain = AddOverlay(Constants.BlockParts.ChainHighlight, blockGo, sprite, OutlineMaterialBottom);
            Image value = AddOverlay(Constants.BlockParts.ValueHighlight, blockGo, sprite, OutlineMaterialRight);
            return (outline, chain, value);
        }

        /// <summary>
        /// 블록과 같은 크기의 투명한 하이라이트 오버레이 이미지 하나를 만들어 반환한다.
        /// 이미지를 늘리지 않고 BlockOutlineMesh가 메시를 테두리 두께만큼 넓힌다.
        /// </summary>
        private static Image AddOverlay(string name, GameObject parent, Sprite sprite, Material mat)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            go.TryGetComponent(out RectTransform rt);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.clear;
            img.raycastTarget = false;
            if (mat) img.material = mat;
            go.AddComponent<BlockOutlineMesh>();
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
        /// 블록 전체를 덮는 중앙 정렬 라벨 TMP를 만든다. 보통은 표시 전용이라 레이캐스트를 받지 않고(드래그는 블록 본체가 받는다),
        /// 배경이 레이캐스트를 받지 않는 ㄷ자 블록 헤더에서는 isHitArea로 라벨이 드래그 영역을 맡는다.
        /// </summary>
        private static async UniTask AddLabel(GameObject go, string text, bool isHitArea = false)
        {
            TMPro.TMP_FontAsset font = await LoadLabelFontAsync();
            GameObject t = new GameObject(Constants.BlockParts.Label, typeof(RectTransform));
            t.SetActive(false);
            t.transform.SetParent(go.transform, false);
            t.TryGetComponent(out RectTransform rt);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI txt = t.AddComponent<TMPro.TextMeshProUGUI>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = Constants.Blocks.LabelFontSize;
            txt.color = Color.white;
            txt.alignment = TMPro.TextAlignmentOptions.Center;
            txt.raycastTarget = isHitArea;
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
