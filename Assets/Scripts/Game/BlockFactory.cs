using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace Game
{
    public static class BlockFactory
    {
        // 이름별로 1회만 Addressables에서 로드하고 이후에는 캐시에서 반환
        private readonly static Dictionary<string, Sprite> _spriteCache = new();

        // 카테고리 + controlRole(Start/End 구분)로 스프라이트 로드
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
                _ => null
            };
            if (name is null) return null;

            if (_spriteCache.TryGetValue(name, out Sprite cached)) return cached;

            Sprite sprite = await Addressables.LoadAssetAsync<Sprite>(name);
            _spriteCache[name] = sprite;
            return sprite;
        }

        // 한글 라벨용 폰트 — Addressables로 로드 후 캐시.
        private static UnityEngine.TextCore.Text.FontAsset _labelFont;

        public static async UniTask<UnityEngine.TextCore.Text.FontAsset> LoadLabelFontAsync()
        {
            if (_labelFont) return _labelFont;
            _labelFont = await Addressables.LoadAssetAsync<UnityEngine.TextCore.Text.FontAsset>(Constants.ResourcePaths.LabelFontKey);
            return _labelFont;
        }

        /// <summary>
        /// 카테고리 버튼 및 스프라이트 미지정 블록의 대체 색상 반환. (색상 값은 Constants.CategoryColors에서 관리)
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
            _ => Constants.CategoryColors.Default
        };

        /// <summary>
        /// 카테고리 선택 버튼에 표시할 한글 이름. (한 곳에서 관리 — 필요 시 이 매핑만 수정)
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
            _ => cat.ToString()
        };

        /// <summary>
        /// 인벤토리 선택 탭 그룹. 기능적으로 다른 카테고리를 하나의 탭으로 묶는다.
        /// (함수 사용/구현은 별도 카테고리지만 '함수' 탭 하나로 표시)
        /// </summary>
        public static BlockCategory GetTabCategory(BlockCategory cat) => cat switch
        {
            BlockCategory.FunctionDef => BlockCategory.Function,
            _ => cat
        };

        // ── 진입점 ──────────────────────────────────────────────
        public static async UniTask<GameObject> Create(BlockEntry entry, Canvas rootCanvas, bool draggable = true)
        {
            GameObject go = entry.category switch
            {
                BlockCategory.Value       => await CreateFromPrefab(Constants.BlockAssets.ValuePrefab, entry, rootCanvas, draggable),
                // 조건 블록 — None: 이벤트형 조건(전용 아트) / kind 지정: 값형 조건(Value 아트 재사용)
                BlockCategory.Condition   => await CreateFromPrefab(entry.valueKind == ValueKind.None
                    ? Constants.BlockAssets.ConditionPrefab
                    : Constants.BlockAssets.ValuePrefab, entry, rootCanvas, draggable),
                // Command + ValueKind.None = 값 슬롯 없는 동작 블록 (전용 아트, ValueOutSocket 미부착)
                BlockCategory.Command     => await CreateFromPrefab(entry.valueKind == ValueKind.None
                    ? Constants.BlockAssets.CommandNoValuePrefab
                    : Constants.BlockAssets.CommandPrefab, entry, rootCanvas, draggable),
                BlockCategory.Control     => await CreateFromPrefab(entry.controlRole == ControlRole.Start
                    ? Constants.BlockAssets.StartPrefab
                    : Constants.BlockAssets.EndPrefab, entry, rootCanvas, draggable),
                // 함수 블록 — CommandNoValue와 동일한 구조(값 슬롯 없음, 체인 소켓만)
                BlockCategory.Function    => await CreateFromPrefab(Constants.BlockAssets.FunctionPrefab, entry, rootCanvas, draggable),
                // 함수 구현 블록 — FlowControl과 동일한 C자 컨테이너(헤더 값 슬롯 없음)
                BlockCategory.FunctionDef => await CreateFromPrefab(Constants.BlockAssets.FuncDefPrefab, entry, rootCanvas, draggable),
                BlockCategory.FlowControl => await CreateFlowBlock(entry, rootCanvas, draggable),
                BlockCategory.Logic       => await CreateLogicBlock(entry, rootCanvas, draggable),
                _                         => await CreateSimpleBlock(entry, rootCanvas, draggable)
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
        /// 이름으로 블록 프리팹을 어드레서블에서 1회 로드하고 캐시에서 반환.
        /// </summary>
        private static async UniTask<GameObject> LoadPrefabAsync(string name)
        {
            if (_prefabCache.TryGetValue(name, out GameObject cached)) return cached;

            GameObject prefab = await Addressables.LoadAssetAsync<GameObject>(name);
            _prefabCache[name] = prefab;
            return prefab;
        }

        /// <summary>
        /// 프리팹을 인스턴스화하고 라벨 텍스트와 CodingBlock 메타를 주입해 블록을 생성.
        /// </summary>
        private static async UniTask<GameObject> CreateFromPrefab(string prefabName, BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            GameObject prefab = await LoadPrefabAsync(prefabName);
            GameObject go = Object.Instantiate(prefab);
            go.name = entry.label; // 컴파일러/채점이 블록 이름으로 값을 읽으므로 라벨과 일치시킨다

            TMPro.TextMeshProUGUI label = go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label)
                label.text = entry.label;
            else
                Debug.LogWarning($"[BlockFactory] {prefabName} 프리팹에 라벨 TMP가 없습니다.");

            if (go.TryGetComponent<CodingBlock>(out CodingBlock block))
            {
                if (draggable)
                    block.Init(entry.category, rootCanvas, entry.valueKind, entry.controlRole);
                else
                    block.enabled = false;
            }
            else
            {
                Debug.LogWarning($"[BlockFactory] {prefabName} 프리팹에 CodingBlock이 없습니다.");
            }

            return go;
        }

        // ── 단순 블록 ───────────────────────────────────────────
        private static async UniTask<GameObject> CreateSimpleBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(entry.category, entry.controlRole);
            
            float w = Constants.Blocks.DefaultWidth;
            float h = Constants.Blocks.DefaultHeight;

            GameObject go = NewRect(entry.label, w, h);
            AddBlockBody(go, GetColor(entry.category), sprite);
            if (go.TryGetComponent<RectTransform>(out RectTransform goRt))
                goRt.sizeDelta = new Vector2(w, h);

            go.AddComponent<CanvasGroup>();
            if (draggable)
            {
                AddDraggable(go, entry, rootCanvas);
            }

            await AddLabel(go, entry.label, (int)Constants.Blocks.LabelFontSize);

            // Logic 블록은 수평 체인 슬롯 포함
            if (entry.category == BlockCategory.Logic && entry.chainBlocks is not null)
                await AppendChain(go, entry.chainBlocks, rootCanvas, draggable);

            return go;
        }

        // ── FlowControl 블록 (C자형) ────────────────────────────
        // 만약/반복하기는 같은 카테고리라 라벨로 아트를 구분한다 (각각 전용 프리팹).
        // 분류 기준(라벨 문자열 비교)은 여기 한 곳에만 있고, 생성 시점(FlowPrefabName)과
        // 부착 시점(AttachSockets)이 같은 함수를 호출해 서로 다른 결과를 낼 수 없게 한다.
        private enum FlowKind { Other, While, If }

        private static FlowKind GetFlowKind(string label) =>
            label == Constants.BlockLabels.While ? FlowKind.While :
            label == Constants.BlockLabels.If    ? FlowKind.If    : FlowKind.Other;

        private static string FlowPrefabName(string label) => GetFlowKind(label) switch
        {
            FlowKind.While => Constants.BlockAssets.WhilePrefab,
            FlowKind.If    => Constants.BlockAssets.IfPrefab,
            _              => Constants.BlockAssets.FlowControlPrefab
        };

        private static async UniTask<GameObject> CreateFlowBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            // 기본 구조(라벨·헤더·Inner·푸터)는 프리팹이 담당
            GameObject go = await CreateFromPrefab(FlowPrefabName(entry.label), entry, rootCanvas, draggable);

            // 사전 배치 블록은 현재 미지원 (런타임 드래그로만 배치)
            if (entry.innerBlocks is not null && entry.innerBlocks.Length > 0)
                Debug.LogWarning("[BlockFactory] 사전 배치 innerBlocks는 아직 지원되지 않습니다.");

            // else 분기 (만약 블록) — 프리팹 기본 구조 뒤에 런타임 추가 후 푸터를 맨 아래로
            if (entry.elseBlocks is not null && entry.elseBlocks.Length > 0)
            {
                await AppendFlowHeader(go.transform, Constants.BlockLabels.Else, Constants.Blocks.FlowElseHeight);
                AppendInnerContainer(go.transform, entry.elseBlocks, rootCanvas, draggable);

                Transform footer = go.transform.Find(Constants.BlockParts.Footer);
                if (footer)
                    footer.SetAsLastSibling();
                else
                    Debug.LogWarning("[BlockFactory] FlowControlBlock 프리팹에 Footer가 없습니다.");
            }

            return go;
        }

        // ── FlowControl 헬퍼 ────────────────────────────────────

        // else 구분 헤더 — 스페이서 + 텍스트 (시각은 Label GO의 9-slice 배경이 담당)
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

        private static void AppendInnerContainer(Transform parent, BlockEntry[] blocks,
            Canvas rootCanvas, bool draggable)
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
            RectTransform socketRt = socketGo.GetComponent<RectTransform>();
            socketRt.anchorMin = socketRt.anchorMax = new Vector2(0.5f, 1f);
            socketRt.pivot = new Vector2(0.5f, 0.5f);
            socketRt.sizeDelta = Vector2.zero;
            socketRt.anchoredPosition = Constants.Sockets.FlowInner;
            InnerSocket innerSocket = socketGo.AddComponent<InnerSocket>();

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
            RectTransform bottomRt = bottomSocketGo.GetComponent<RectTransform>();
            bottomRt.anchorMin = bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0.5f);
            bottomRt.sizeDelta = Vector2.zero;
            bottomRt.anchoredPosition = Constants.Sockets.FlowInnerBottom;
            bottomSocketGo.AddComponent<InnerBottomSocket>();

            // 사전 배치 블록은 현재 미지원 (런타임 드래그로만 배치)
            if (blocks is not null && blocks.Length > 0)
                Debug.LogWarning("[BlockFactory] 사전 배치 innerBlocks는 아직 지원되지 않습니다.");

            inner.AddComponent<FlowInnerResize>();
        }

        // ── Logic 블록 (그리고 / 또는) ───────────────────────────────────
        // 단순 레이블 블록 — ConditionIn/Out 소켓으로 조건 체인에 연결
        private static async UniTask<GameObject> CreateLogicBlock(BlockEntry entry, Canvas rootCanvas, bool draggable)
        {
            Sprite sprite = await LoadSpriteAsync(BlockCategory.Logic);
            GameObject go = NewRect(entry.label, 120f, 56f);
            AddBlockBody(go, GetColor(BlockCategory.Logic), sprite);

            // 스프라이트 원본 크기 기준 확대 — transform 스케일 대신 크기·라벨을 함께 키움
            if (sprite && go.TryGetComponent<RectTransform>(out RectTransform rt))
                rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * Constants.Blocks.LogicScale;

            go.AddComponent<CanvasGroup>();
            if (draggable)
                AddDraggable(go, entry, rootCanvas);
            await AddLabel(go, entry.label, (int)Constants.Blocks.LabelFontSize);
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
            // ValueKind.None인 Command는 값 슬롯 없는 블록이므로 소켓을 붙이지 않는다 (Value 스냅 불가)
            if (cat == BlockCategory.Command && block.ValueKind != ValueKind.None)
                AttachValueOutSocket(block.gameObject, Constants.Sockets.CommandValueOut);

            // Logic은 전용 아트(Logic)라 좌측 노치 오프셋이 Condition과 다르다
            if (cat == BlockCategory.Value)
                AttachValueInSocket(block.gameObject, Constants.Sockets.ValueValueIn);
            else if (cat == BlockCategory.Logic)
                AttachValueInSocket(block.gameObject, Constants.Sockets.LogicConditionIn);
            else if (cat == BlockCategory.Condition)
                AttachValueInSocket(block.gameObject, Constants.Sockets.ConditionValueIn);

            // Condition / Logic 블록: 수평 조건 체인 소켓
            if (cat == BlockCategory.Logic)
            {
                AttachConditionInSocket(block.gameObject,  Constants.Sockets.LogicConditionIn);
                AttachConditionOutSocket(block.gameObject, Constants.Sockets.LogicConditionOut);
            }
            else if (cat == BlockCategory.Condition)
            {
                AttachConditionInSocket(block.gameObject,  Constants.Sockets.ConditionIn);
                AttachConditionOutSocket(block.gameObject, Constants.Sockets.ConditionOut);
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
                bool useNoValueOffset = isNoValueCommand || cat == BlockCategory.Function;

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

                if (!isEnd)   AttachOutSocket(block.gameObject, outOffset);
                if (!isStart) AttachInSocket(block.gameObject,  inOffset);
            }
        }

        // ── 블록에 ChainOutSocket 후부착 — 앵커 (0.5, 0) 하단 중앙
        public static void AttachOutSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find(Constants.Sockets.ChainOutName)) return;

            GameObject go = new GameObject(Constants.Sockets.ChainOutName, typeof(RectTransform));
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
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
            if (block.transform.Find(Constants.Sockets.ChainInName)) return;

            GameObject go = new GameObject(Constants.Sockets.ChainInName, typeof(RectTransform));
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
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
            if (commandBlock.transform.Find(Constants.Sockets.ValueOutName)) return;

            GameObject go = new GameObject(Constants.Sockets.ValueOutName, typeof(RectTransform));
            go.transform.SetParent(commandBlock.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
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
            if (valueBlock.transform.Find(Constants.Sockets.ValueInName)) return;

            GameObject go = new GameObject(Constants.Sockets.ValueInName, typeof(RectTransform));
            go.transform.SetParent(valueBlock.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
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
            if (block.transform.Find(Constants.Sockets.ConditionOutName)) return;

            GameObject go = new GameObject(Constants.Sockets.ConditionOutName, typeof(RectTransform));
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ConditionOutSocket>();
        }

        public static void AttachConditionInSocket(GameObject block, Vector2 offset = default)
        {
            if (block.transform.Find(Constants.Sockets.ConditionInName)) return;

            GameObject go = new GameObject(Constants.Sockets.ConditionInName, typeof(RectTransform));
            go.transform.SetParent(block.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = offset;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.AddComponent<ConditionInSocket>();
        }

        // ── 빈 CodingSlot ───────────────────────────────────────
        // 비어있는 슬롯(EmptyIndicator·CodingSlot) 공통 높이
        private const float EmptySlotHeight = 60f;

        public static GameObject CreateEmptyCodingSlot()
        {
            GameObject go = NewRect(Constants.BlockParts.Slot, 0f, EmptySlotHeight);
            AddImage(go, Constants.HighlightColors.EmptySlot);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = EmptySlotHeight;
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
        private static Material _spriteFillMaterialInner;
        private static Material _spriteFillMaterialRight;

        public static Material SpriteFillMaterial       => GetOrLoadMaterial(ref _spriteFillMaterial,       "BlockOutlineFull",   0.0f, 1.0f, 0.0f, 1.0f);
        public static Material SpriteFillMaterialBottom => GetOrLoadMaterial(ref _spriteFillMaterialBottom, "BlockOutlineBottom", 0.0f, Constants.HighlightSettings.ChainHighlightYMax, 0.0f, 1.0f);
        public static Material SpriteFillMaterialInner  => GetOrLoadMaterial(ref _spriteFillMaterialInner,  "BlockOutlineInner",  0.0f, Constants.HighlightSettings.InnerHighlightYMax, 0.0f, 1.0f);
        public static Material SpriteFillMaterialRight  => GetOrLoadMaterial(ref _spriteFillMaterialRight,  "BlockOutlineRight",  0.0f, 1.0f, Constants.HighlightSettings.ValueHighlightXMin, 1.0f);

        private static Material GetOrLoadMaterial(ref Material cache, string matName, float yMin, float yMax, float xMin, float xMax)
        {
            if (!cache) cache = MakeSpriteFillMat(matName, yMin, yMax, xMin, xMax);
            return cache;
        }

        private static Material MakeSpriteFillMat(string matName, float yMin, float yMax, float xMin, float xMax)
        {
            Shader shader = Shader.Find(Constants.ResourcePaths.SpriteFillShader);
            if (!shader)
            {
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

                GameObject spriteGo = new GameObject(Constants.BlockParts.Sprite, typeof(RectTransform));
                spriteGo.transform.SetParent(go.transform, false);
                RectTransform srt = spriteGo.GetComponent<RectTransform>();
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

                GameObject fillGo = new GameObject(Constants.BlockParts.Fill, typeof(RectTransform));
                fillGo.transform.SetParent(go.transform, false);
                RectTransform frt = fillGo.GetComponent<RectTransform>();
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
                fillGo.AddComponent<Image>().color = color;
            }
        }

        // 스프라이트 블록용 방향별 하이라이트 오버레이 3종 생성 (기본 투명, 런타임에 색 변경)
        // sibling 0: SpriteOutline  — 전체 (컴파일 에러 → 빨간색)
        // sibling 1: ChainHighlight — 셰이더로 하단 영역만 표시 (체인 스냅 → 초록색)
        // sibling 2: ValueHighlight — 셰이더로 우측 영역만 표시 (값 스냅 → 초록색)
        private static void AddHighlightOverlays(GameObject blockGo, Sprite sprite)
        {
            float t = Constants.HighlightSettings.OutlineThickness;
            var minOffFull = new Vector2(-t, -t);
            var maxOffFull = new Vector2(t, t);

            var minOffChain = new Vector2(0f, -t);
            var maxOffChain = new Vector2(0f, t);

            AddOverlay(Constants.BlockParts.Outline, blockGo, sprite, Vector2.zero, Vector2.one, minOffFull, maxOffFull, SpriteFillMaterial);
            AddOverlay(Constants.BlockParts.ChainHighlight, blockGo, sprite, Vector2.zero, Vector2.one, minOffChain, maxOffChain,
                SpriteFillMaterialBottom);
            AddOverlay(Constants.BlockParts.ValueHighlight, blockGo, sprite, Vector2.zero, Vector2.one, minOffFull, maxOffFull,
                SpriteFillMaterialRight);
        }

        private static void AddOverlay(string name, GameObject parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Material mat = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
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
            GameObject border = new GameObject(Constants.BlockParts.Outline, typeof(RectTransform));
            border.transform.SetParent(go.transform, false);
            border.transform.SetAsFirstSibling();
            RectTransform rt = border.GetComponent<RectTransform>();
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
            GameObject t = new GameObject(Constants.BlockParts.Label, typeof(RectTransform));
            t.transform.SetParent(go.transform, false);
            RectTransform rt = t.GetComponent<RectTransform>();
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
