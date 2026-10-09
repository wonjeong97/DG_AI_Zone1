using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace Scenes
{
    [RequireComponent(typeof(Image))]
    public class TutorialImageSlider : MonoBehaviour, IPointerClickHandler
    {
        // 화면 오른쪽 절반을 누르면 다음, 왼쪽 절반을 누르면 이전 페이지
        private const float NextPageThreshold = 0.5f;

        [Tooltip("튜토리얼 이미지 장수 — Addressables 주소 Tutorial1 ~ TutorialN")]
        [SerializeField] private int totalPages = 7;
        [SerializeField] private TMP_Text pageText;

        // 마지막 페이지에서 다음으로 넘기려 할 때 발생 — 인트로 씬이 스토리 씬으로 넘어가는 신호로 쓴다
        public event Action Finished;

        // 페이지별로 1회만 Addressables에서 로드하고 이후에는 같은 핸들의 결과를 쓴다 — 씬을 떠날 때 모두 해제한다
        private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _spriteHandles = new();

        private Image _image;
        private RectTransform _rectTransform;
        private int _currentIndex;
        private ILogger<TutorialImageSlider> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거와 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<TutorialImageSlider> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 이미지와 RectTransform 참조를 캐싱한다 (씬 주입은 Awake 뒤라 실패 로그는 Start에서 남긴다).
        /// </summary>
        private void Awake()
        {
            TryGetComponent(out _image);
            _rectTransform = (RectTransform)transform;
        }

        /// <summary>
        /// 첫 페이지를 표시한다.
        /// </summary>
        private void Start()
        {
            if (!_image && _logger != null)
                _logger.ZLogError($"[TutorialImageSlider] {name}에 Image가 없습니다.");

            _currentIndex = 0;
            UpdatePageAsync().Forget();
        }

        /// <summary>
        /// 불러온 튜토리얼 이미지를 해제한다 — 인트로 씬을 다시 열 때마다 새로 불러오므로 남겨 두면 참조만 쌓인다.
        /// </summary>
        private void OnDestroy()
        {
            foreach (AsyncOperationHandle<Sprite> handle in _spriteHandles.Values)
                if (handle.IsValid()) Addressables.Release(handle);
            _spriteHandles.Clear();
        }

        /// <summary>
        /// 이미지의 오른쪽 절반을 누르면 다음, 왼쪽 절반을 누르면 이전 페이지로 넘긴다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            Rect rect = _rectTransform.rect;
            float normalizedX = (localPoint.x - rect.xMin) / rect.width;

            if (normalizedX >= NextPageThreshold) ShowNext();
            else ShowPrevious();
        }

        /// <summary>
        /// 다음 페이지로 넘긴다 (마지막 페이지에서는 넘기지 않고 Finished를 알린다).
        /// </summary>
        private void ShowNext()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);

            if (_currentIndex >= totalPages - 1)
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

            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            _currentIndex--;
            UpdatePageAsync().Forget();
        }

        /// <summary>
        /// 현재 페이지 번호와 이미지를 갱신한다 (이미지는 페이지별 1회만 로드).
        /// </summary>
        private async UniTaskVoid UpdatePageAsync()
        {
            int page = _currentIndex + 1;
            if (pageText) pageText.text = ZString.Format(Constants.TutorialMessages.PageFormat, page, totalPages);
            else if (_logger != null) _logger.ZLogWarning($"[TutorialImageSlider] pageText가 할당되지 않아 페이지 번호를 표시하지 못했습니다.");

            if (!_spriteHandles.TryGetValue(page, out AsyncOperationHandle<Sprite> handle))
            {
                handle = Addressables.LoadAssetAsync<Sprite>(ZString.Concat(Constants.ResourcePaths.TutorialImageAddress, page));
                _spriteHandles[page] = handle;
            }

            Sprite sprite;
            try
            {
                sprite = await handle.ToUniTask(cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 불러오는 도중 씬을 떠난 경우 — 핸들은 OnDestroy가 해제한다
                return;
            }
            catch (Exception ex)
            {
                if (_logger != null) _logger.ZLogError($"[TutorialImageSlider] 튜토리얼 이미지 {page}장을 불러오지 못했습니다: {ex.Message}");
                return;
            }

            // 로딩 중 다른 페이지로 이동했다면(연속 클릭) 결과가 최신 페이지를 덮어쓰지 않도록 방지
            if (_currentIndex + 1 == page && _image) _image.sprite = sprite;
        }
    }
}
