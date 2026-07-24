using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using VContainer;
using ZLogger;

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

        [Inject] private ILogger<CategoryZone> _log;

        private static GameObject _buttonPrefab;
        private readonly List<(BlockCategory cat, Image fillImg, TMPro.TextMeshProUGUI labelText)> _buttons = new();

        // 인벤토리에 존재하는 카테고리 순서대로 버튼 생성 후 첫 카테고리 활성화
        public async UniTask Build(IReadOnlyList<BlockCategory> categories)
        {
            // 가로정렬 대신 3x2 그리드 레이아웃 설정
            if (!buttonContainer.TryGetComponent<GridLayoutGroup>(out GridLayoutGroup grid))
            {
                if (buttonContainer.TryGetComponent<HorizontalLayoutGroup>(out HorizontalLayoutGroup hlg))
                    DestroyImmediate(hlg);
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

            if (!_buttonPrefab)
            {
                _buttonPrefab = await Addressables.LoadAssetAsync<GameObject>("CategoryButton");
            }

            foreach (BlockCategory cat in categories)
            {
                var (fillImg, txt) = CreateButton(cat);
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
                    child.gameObject.SetActive(BlockFactory.GetTabCategory(block.Category) == cat);

            // 스크롤이 내려간 상태에서 콘텐츠가 짧은 카테고리로 바뀌면 Content가 범위 밖에 남아
            // 스크롤바 핸들 크기가 0으로 계산되므로, 전환 시 레이아웃 갱신 후 맨 위로 리셋
            ScrollRect scrollRect = inventoryContent.GetComponentInParent<ScrollRect>();
            if (scrollRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
                scrollRect.verticalNormalizedPosition = 1f;
            }
            else
            {
                _log?.ZLogWarning($"[CategoryZone] 인벤토리 ScrollRect를 찾지 못해 스크롤 리셋을 건너뜁니다.");
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

        private (Image fillImg, TMPro.TextMeshProUGUI labelText) CreateButton(BlockCategory cat)
        {
            GameObject go = Instantiate(_buttonPrefab, buttonContainer, false);
            go.name = cat + "Button";

            Image fillImg = null;
            TMPro.TextMeshProUGUI labelText = null;

            if (go.TryGetComponent<CategoryButtonUI>(out CategoryButtonUI ui))
            {
                fillImg = ui.FillImage;
                labelText = ui.LabelText;

                if (ui.Button)
                {
                    BlockCategory captured = cat;
                    ui.Button.onClick.RemoveAllListeners();
                    ui.Button.onClick.AddListener(() => Select(captured));
                }
            }
            else
            {
                Debug.LogWarning($"[CategoryZone] CategoryButton 프리팹에 CategoryButtonUI 컴포넌트가 없습니다.");
            }

            if (labelText)
                labelText.text = BlockFactory.GetCategoryName(cat);

            if (fillImg)
                fillImg.color = BlockFactory.GetColor(cat);

            return (fillImg, labelText);
        }
    }
}
