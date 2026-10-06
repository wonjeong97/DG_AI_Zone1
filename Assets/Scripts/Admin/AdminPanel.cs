using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace Admin
{
    // 비밀번호를 통과하면 열리는 관리자 화면 — 지금은 닫기만 있고, 관리 기능은 이 패널 안에 추가한다.
    public class AdminPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        private ILogger<AdminPanel> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거와 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<AdminPanel> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 닫기 버튼에 닫기 동작을 연결한다 (패널이 처음 켜질 때 한 번 실행).
        /// </summary>
        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null)
                _logger.ZLogWarning($"[AdminPanel] closeButton이 할당되지 않아 관리자 화면을 닫을 수 없습니다.");
        }

        /// <summary>
        /// 닫기 버튼 연결을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>
        /// 관리자 화면을 연다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 관리자 화면을 열었습니다.");
        }

        /// <summary>
        /// 닫기 효과음을 내고 관리자 화면을 닫는다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            gameObject.SetActive(false);
        }
    }
}
