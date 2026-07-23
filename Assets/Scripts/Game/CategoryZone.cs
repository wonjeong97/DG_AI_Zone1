using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Game
{
    // 카테고리 존 — 인벤토리에 존재하는 카테고리별 선택 버튼을 코드로 생성하고,
    // 선택된 카테고리의 블록만 인벤토리에 표시(show/hide)한다. (Control 블록은 코딩존이라 대상 아님)
    public class CategoryZone : MonoBehaviour
    {
        [SerializeField] private RectTransform buttonContainer; // 버튼이 담길 컨테이너 (레이아웃 그룹 포함)
        [SerializeField] private Transform inventoryContent;    // 인벤토리 블록들의 부모(Content)

        public Transform InventoryContent => inventoryContent;
        public BlockCategory CurrentCategory { get; private set; }

        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 60f;

        private readonly List<(BlockCategory cat, Image fillImg, TMPro.TextMeshProUGUI labelText)> _buttons = new();

        // 인벤토리에 존재하는 카테고리 순서대로 버튼 생성 후 첫 카테고리 활성화
        public async UniTask Build(IReadOnlyList<BlockCategory> categories)
        {
            // 가로정렬 대신 3x2 그리드 레이아웃 설정
            var grid = buttonContainer.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                var hlg = buttonContainer.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null) DestroyImmediate(hlg);
                grid = buttonContainer.gameObject.AddComponent<GridLayoutGroup>();
            }
            grid.cellSize = new Vector2(138f, 42f);
            grid.spacing = new Vector2(8f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.padding = new RectOffset(8, 8, 4, 4);

            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
                Destroy(buttonContainer.GetChild(i).gameObject);
            _buttons.Clear();

            UnityEngine.TextCore.Text.FontAsset font = await BlockFactory.LoadLabelFontAsync();

            foreach (BlockCategory cat in categories)
            {
                var (fillImg, txt) = CreateButton(cat, font);
                _buttons.Add((cat, fillImg, txt));
            }

            if (categories.Count > 0)
                Select(categories[0]);
        }

        public void Select(BlockCategory cat)
        {
            CurrentCategory = cat;
            foreach (Transform child in inventoryContent)
                if (child.TryGetComponent<CodingBlock>(out CodingBlock block))
                    child.gameObject.SetActive(block.Category == cat);

            // 스크롤이 내려간 상태에서 콘텐츠가 짧은 카테고리로 바뀌면 Content가 범위 밖에 남아
            // 스크롤바 핸들 크기가 0으로 계산되므로, 전환 시 레이아웃 갱신 후 맨 위로 리셋
            ScrollRect scrollRect = inventoryContent.GetComponentInParent<ScrollRect>();
            if (scrollRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
                scrollRect.verticalNormalizedPosition = 1f;
            }

            foreach ((BlockCategory c, Image fillImg, TMPro.TextMeshProUGUI labelText) in _buttons)
            {
                bool selected = (c == cat);
                if (fillImg != null)
                    fillImg.color = Tint(BlockFactory.GetColor(c), selected);
                if (labelText != null)
                    labelText.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
        }

        // 선택: 원색 / 비선택: 어둡게
        private static Color Tint(Color baseColor, bool selected)
            => selected ? baseColor : baseColor * 0.55f;

        private (Image fillImg, TMPro.TextMeshProUGUI labelText) CreateButton(BlockCategory cat, UnityEngine.TextCore.Text.FontAsset font)
        {
            GameObject go = new GameObject(cat + "Button");
            go.transform.SetParent(buttonContainer, false);
            go.AddComponent<RectTransform>();

            Image bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.001f); // 터치/클릭 감지용 투명 배경

            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            BlockCategory captured = cat;
            go.AddComponent<Button>().onClick.AddListener(() => Select(captured));

            // 네모 아이콘 (외곽선 + 내부 색상)
            GameObject colorBoxGo = new GameObject("ColorBox");
            colorBoxGo.transform.SetParent(go.transform, false);
            colorBoxGo.AddComponent<RectTransform>().sizeDelta = new Vector2(31f, 31f);
            Image borderImg = colorBoxGo.AddComponent<Image>();
            borderImg.color = new Color(0.25f, 0.25f, 0.28f, 1f);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(colorBoxGo.transform, false);
            RectTransform fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = BlockFactory.GetColor(cat);

            // 라벨 텍스트
            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            textGo.AddComponent<RectTransform>().sizeDelta = new Vector2(88f, 42f);
            TMPro.TextMeshProUGUI txt = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            txt.text = BlockFactory.GetCategoryName(cat);
            txt.font = font;
            txt.fontSize = 24;
            txt.color = Color.white;
            txt.alignment = TMPro.TextAlignmentOptions.MidlineLeft;

            return (fillImg, txt);
        }
    }
}
