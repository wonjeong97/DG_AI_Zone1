using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Scenes
{
    [RequireComponent(typeof(Image))]
    public class TutorialImageSlider : MonoBehaviour, IPointerClickHandler
    {
        private const int TotalPages = 7;

        [SerializeField] private TMP_Text pageText;

        // 페이지별로 1회만 Addressables에서 로드하고 이후에는 캐시에서 반환
        private readonly Dictionary<int, Sprite> _spriteCache = new();

        private Image _image;
        private RectTransform _rectTransform;
        private int _currentIndex;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = (RectTransform)transform;
        }

        private void Start()
        {
            _currentIndex = 0;
            UpdatePageAsync().Forget();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            Rect rect = _rectTransform.rect;
            float normalizedX = (localPoint.x - rect.xMin) / rect.width;

            if (normalizedX >= 0.5f) ShowNext();
            else ShowPrevious();
        }

        private void ShowNext()
        {
            _currentIndex = (_currentIndex + 1) % TotalPages;
            UpdatePageAsync().Forget();
        }

        private void ShowPrevious()
        {
            _currentIndex = (_currentIndex - 1 + TotalPages) % TotalPages;
            UpdatePageAsync().Forget();
        }

        private async UniTaskVoid UpdatePageAsync()
        {
            int page = _currentIndex + 1;
            if (pageText) pageText.text = $"튜토리얼 ({page}/{TotalPages})";

            if (!_spriteCache.TryGetValue(page, out Sprite sprite))
            {
                sprite = await Addressables.LoadAssetAsync<Sprite>($"{Constants.ResourcePaths.TutorialImageAddress}{page}");
                _spriteCache[page] = sprite;
            }

            // 로딩 중 다른 페이지로 이동했다면(연속 클릭) 결과가 최신 페이지를 덮어쓰지 않도록 방지
            if (_currentIndex + 1 == page) _image.sprite = sprite;
        }
    }
}
