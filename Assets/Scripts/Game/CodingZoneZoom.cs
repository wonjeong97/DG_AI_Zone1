using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Game
{
    // 코딩 패널 확대/축소 — 두 손가락 핀치와 마우스 휠. 빈 곳 드래그 이동은 ScrollRect가 맡는다.
    // 배율은 Content(CodingZone)의 localScale로 적용하고, 손가락 중심·마우스 위치 아래 지점이 제자리에 머물도록 위치를 보정한다.
    // Content는 Viewport 좌상단에 앵커·피벗이 맞춰져 있다고 가정하고 이동 범위를 제한한다 (3_Game 씬의 CodingPanel 구성).
    [RequireComponent(typeof(ScrollRect))]
    public class CodingZoneZoom : MonoBehaviour, IScrollHandler
    {
        [Tooltip("최대 확대 배율 (최소 배율은 코딩 판이 뷰포트를 꽉 채우는 크기로 자동 계산)")]
        [SerializeField] private float maxZoom = 1.5f;

        [Tooltip("마우스 휠 한 칸당 배율 변화 비율")]
        [SerializeField] private float wheelZoomStep = 1.1f;

        private ScrollRect _scrollRect;
        private RectTransform _content;
        private RectTransform _viewport;
        private Camera _eventCamera;

        private bool _isPinching;
        private float _lastPinchDistance;

        // 핀치 중에는 한 손가락 이동을 막았다가, 손가락을 모두 떼면 원래 설정으로 되돌린다
        private bool _defaultHorizontal;
        private bool _defaultVertical;

        /// <summary>
        /// 스크롤뷰의 Content·Viewport와 좌표 변환에 쓸 카메라를 캐싱한다.
        /// </summary>
        private void Awake()
        {
            if (!TryGetComponent(out _scrollRect))
            {
                Debug.LogError($"[CodingZoneZoom] {name}에 ScrollRect가 없습니다.");
                enabled = false;
                return;
            }

            _content = _scrollRect.content;
            _viewport = _scrollRect.viewport ? _scrollRect.viewport : (RectTransform)transform;
            if (!_content)
            {
                Debug.LogError($"[CodingZoneZoom] {name}의 ScrollRect에 Content가 지정되지 않았습니다.");
                enabled = false;
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _eventCamera = canvas.rootCanvas.worldCamera;

            _defaultHorizontal = _scrollRect.horizontal;
            _defaultVertical = _scrollRect.vertical;
        }

        // 코딩 판이 뷰포트보다 작아지면 빈 영역이 드러나므로 뷰포트를 꽉 채우는 배율까지만 축소한다
        private float MinZoom
        {
            get
            {
                Rect content = _content.rect;
                Rect viewport = _viewport.rect;
                if (content.width <= 0f || content.height <= 0f) return 1f;
                return Mathf.Min(1f, Mathf.Max(viewport.width / content.width, viewport.height / content.height));
            }
        }

        /// <summary>
        /// 마우스 휠을 올리면 확대, 내리면 축소한다 (휠 스크롤 이동 대신).
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            float wheel = eventData.scrollDelta.y;
            if (Mathf.Abs(wheel) < 0.001f) return;

            // 장치마다 휠 한 칸의 값이 달라 방향만 쓴다
            ZoomAt(eventData.position, wheel > 0f ? wheelZoomStep : 1f / wheelZoomStep);
        }

        /// <summary>
        /// 터치스크린에 두 손가락 이상이 닿아 있는지 — 핀치 중에는 블록을 집지 못하게 하는 데 쓴다.
        /// </summary>
        public static bool IsMultiTouch => CountPressedTouches(out _, out _) >= 2;

        /// <summary>
        /// 눌린 터치 수를 세고, 앞의 두 손가락 위치를 돌려준다.
        /// </summary>
        private static int CountPressedTouches(out Vector2 first, out Vector2 second)
        {
            first = default;
            second = default;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return 0;

            int pressedCount = 0;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed) continue;

                if (pressedCount == 0) first = touch.position.ReadValue();
                else if (pressedCount == 1) second = touch.position.ReadValue();
                pressedCount++;
            }
            return pressedCount;
        }

        /// <summary>
        /// 두 손가락이 코딩 패널 안에서 벌어지거나 좁혀지는 만큼 확대/축소한다.
        /// </summary>
        private void Update()
        {
            int pressedCount = CountPressedTouches(out Vector2 first, out Vector2 second);

            if (pressedCount >= 2)
            {
                float distance = Vector2.Distance(first, second);
                if (!_isPinching)
                {
                    // 두 손가락이 모두 코딩 패널 안에 있을 때만 핀치로 본다 (인벤토리 쪽 터치와 섞이지 않게)
                    if (!IsInsideViewport(first) || !IsInsideViewport(second)) return;

                    _isPinching = true;
                    _lastPinchDistance = distance;
                    SetPanEnabled(false);

                    // 첫 손가락이 블록을 집고 있었다면 그 블록은 옮기지 않고 제자리로 돌려놓는다
                    CodingBlock.CancelActiveDrags();
                    return;
                }

                // 핀치 도중 한 손가락을 뗀 사이에 남은 손가락으로 블록을 집었을 수 있으므로, 두 손가락이 닿아 있는 동안 계속 되돌린다
                CodingBlock.CancelActiveDrags();

                if (_lastPinchDistance > 0f)
                    ZoomAt((first + second) * 0.5f, distance / _lastPinchDistance);
                _lastPinchDistance = distance;
            }
            else if (_isPinching && pressedCount == 1)
            {
                // 한 손가락을 뗐다가 다시 대면 다른 두 점 사이 거리와 비교해 배율이 튀므로, 다음 두 손가락 입력을 새 기준으로 삼는다
                _lastPinchDistance = 0f;
            }
            else if (_isPinching && pressedCount == 0)
            {
                // 한 손가락만 남은 상태에서 이동을 켜면 ScrollRect가 핀치 전 시작점을 기준으로 튀므로 모두 뗄 때까지 기다린다
                _isPinching = false;
                SetPanEnabled(true);
            }
        }

        /// <summary>
        /// 화면 좌표가 코딩 패널 뷰포트 안인지 확인한다.
        /// </summary>
        private bool IsInsideViewport(Vector2 screenPoint)
            => RectTransformUtility.RectangleContainsScreenPoint(_viewport, screenPoint, _eventCamera);

        /// <summary>
        /// 빈 곳 드래그 이동을 켜거나 끈다. 끌 때는 남은 관성도 멈춘다.
        /// </summary>
        private void SetPanEnabled(bool isEnabled)
        {
            _scrollRect.horizontal = isEnabled && _defaultHorizontal;
            _scrollRect.vertical = isEnabled && _defaultVertical;
            if (!isEnabled) _scrollRect.StopMovement();
        }

        /// <summary>
        /// 화면 좌표 아래 지점을 고정한 채 배율을 factor만큼 바꾸고, 코딩 판이 뷰포트 밖으로 벗어나지 않게 위치를 제한한다.
        /// </summary>
        private void ZoomAt(Vector2 screenPoint, float factor)
        {
            float current = _content.localScale.x;
            float target = Mathf.Clamp(current * factor, MinZoom, maxZoom);
            if (Mathf.Approximately(target, current)) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPoint, _eventCamera, out Vector2 before);
            _content.localScale = new Vector3(target, target, 1f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPoint, _eventCamera, out Vector2 after);

            // 배율이 바뀌며 화면 좌표 아래에서 밀려난 로컬 지점만큼 되돌려, 같은 지점이 계속 손가락·커서 아래에 있게 한다
            _content.anchoredPosition += (after - before) * target;
            _scrollRect.StopMovement();
            ClampContentPosition();
        }

        /// <summary>
        /// 확대된 코딩 판이 뷰포트를 항상 덮도록 위치를 제한한다 (좌상단 앵커·피벗 기준).
        /// </summary>
        private void ClampContentPosition()
        {
            float scale = _content.localScale.x;
            Rect viewport = _viewport.rect;
            Vector2 size = _content.rect.size * scale;

            Vector2 pos = _content.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, Mathf.Min(0f, viewport.width - size.x), 0f);
            pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, size.y - viewport.height));
            _content.anchoredPosition = pos;
        }
    }
}
