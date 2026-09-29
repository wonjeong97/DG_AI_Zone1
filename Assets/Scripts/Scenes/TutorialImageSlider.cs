using System.Collections.Generic;
using Cysharp.Text;
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

        // 마지막 페이지에서 다음으로 넘기려 할 때 발생 — 인트로 씬이 스토리 씬으로 넘어가는 신호로 쓴다
        public event System.Action Finished;

        // 페이지별로 1회만 Addressables에서 로드하고 이후에는 캐시에서 반환
        private readonly Dictionary<int, Sprite> _spriteCache = new();

        private Image _image;
        private RectTransform _rectTransform;
        private int _currentIndex;

        /// <summary>
        /// 이미지와 RectTransform 참조를 캐싱한다.
        /// </summary>
        private void Awake()
        {
            if (!TryGetComponent(out _image))
                Debug.LogError($"[TutorialImageSlider] {name}에 Image가 없습니다.");
            _rectTransform = (RectTransform)transform;
        }

        /// <summary>
        /// 첫 페이지를 표시한다.
        /// </summary>
        private void Start()
        {
            _currentIndex = 0;
            UpdatePageAsync().Forget();
        }

        /// <summary>
        /// 이미지의 오른쪽 절반을 누르면 다음, 왼쪽 절반을 누르면 이전 페이지로 넘긴다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            Rect rect = _rectTransform.rect;
            float normalizedX = (localPoint.x - rect.xMin) / rect.width;

            if (normalizedX >= 0.5f) ShowNext();
            else ShowPrevious();
        }

        /// <summary>
        /// 다음 페이지로 넘긴다 (마지막 페이지에서는 넘기지 않고 Finished를 알린다).
        /// </summary>
        private void ShowNext()
        {
            if (_currentIndex == TotalPages - 1)
            {
                Finished?.Invoke();
                return;
            }

            _currentIndex++;
            UpdatePageAsync().Forget();
        }

        /// <summary>
        /// 이전 페이지로 넘긴다 (첫 페이지에서는 넘기지 않는다 — 마지막으로 순회하면 튜토리얼을 건너뛸 수 있으므로).
        /// </summary>
        private void ShowPrevious()
        {
            if (_currentIndex == 0) return;

            _currentIndex--;
            UpdatePageAsync().Forget();
        }

        /// <summary>
        /// 현재 페이지 번호와 이미지를 갱신한다 (이미지는 페이지별 1회만 로드).
        /// </summary>
        private async UniTaskVoid UpdatePageAsync()
        {
            int page = _currentIndex + 1;
            if (pageText) pageText.text = ZString.Concat("튜토리얼 (", page, "/", TotalPages, ")");

            if (!_spriteCache.TryGetValue(page, out Sprite sprite))
            {
                sprite = await Addressables.LoadAssetAsync<Sprite>(ZString.Concat(Constants.ResourcePaths.TutorialImageAddress, page));
                _spriteCache[page] = sprite;
            }

            // 로딩 중 다른 페이지로 이동했다면(연속 클릭) 결과가 최신 페이지를 덮어쓰지 않도록 방지
            if (_currentIndex + 1 == page && _image) _image.sprite = sprite;
        }
    }
}
