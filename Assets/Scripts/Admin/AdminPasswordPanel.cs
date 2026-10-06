using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace Admin
{
    // 관리자 비밀번호 입력 창 — 키보드·마우스가 없는 전시 환경이라 화면 키패드(789/456/123/확인0←)로 입력받는다.
    // 비밀번호가 맞으면 관리자 화면을 열고, 닫기 버튼을 누르거나 일정 시간 입력이 없으면 닫힌다.
    public class AdminPasswordPanel : MonoBehaviour
    {
        [Tooltip("숫자 키 0~9 — 배열 인덱스가 그 키가 입력하는 숫자다")]
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backspaceButton;
        [SerializeField] private Button closeButton;

        [Tooltip("입력한 자릿수만큼 ●를 표시하는 텍스트")]
        [SerializeField] private TMP_Text maskedText;
        [Tooltip("자릿수 부족·비밀번호 오류 안내 텍스트")]
        [SerializeField] private TMP_Text messageText;

        [SerializeField] private AdminPanel adminPanel;

        [Tooltip("이 시간(초) 동안 키패드 입력이 없으면 창을 닫는다")]
        [SerializeField, Min(1f)] private float idleTimeout = 10f;

        // 자릿수별 표시 문자열 — 키를 누를 때마다 문자열을 새로 만들지 않도록 미리 만들어 둔다
        private readonly static string[] MaskTexts = CreateMaskTexts();

        private readonly PasswordInput _input = new();
        private UnityAction[] _digitActions;
        private string _password = Constants.Admin.DefaultPassword;
        private float _lastInputTime;

        private ILogger<AdminPasswordPanel> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거와 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<AdminPasswordPanel> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 키패드 버튼에 입력 동작을 연결한다 (패널이 처음 켜질 때 한 번 실행).
        /// </summary>
        private void Awake()
        {
            _digitActions = new UnityAction[digitButtons.Length];
            for (int i = 0; i < digitButtons.Length; i++)
            {
                if (!digitButtons[i])
                {
                    if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] 숫자 {i} 버튼이 할당되지 않았습니다.");
                    continue;
                }

                int digit = i;
                _digitActions[i] = () => OnDigitClicked(digit);
                digitButtons[i].onClick.AddListener(_digitActions[i]);
            }

            if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] confirmButton이 할당되지 않았습니다.");

            if (backspaceButton) backspaceButton.onClick.AddListener(OnBackspaceClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] backspaceButton이 할당되지 않았습니다.");

            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] closeButton이 할당되지 않았습니다.");
        }

        /// <summary>
        /// 키패드 버튼 연결을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_digitActions != null)
                for (int i = 0; i < digitButtons.Length; i++)
                    if (digitButtons[i] && _digitActions[i] != null) digitButtons[i].onClick.RemoveListener(_digitActions[i]);

            if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (backspaceButton) backspaceButton.onClick.RemoveListener(OnBackspaceClicked);
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>
        /// 입력을 비운 채 창을 열고, 현장에서 바뀌었을 수 있는 비밀번호를 파일에서 다시 읽는다.
        /// </summary>
        public void Open()
        {
            _input.Clear();
            RefreshMasked();
            SetMessage(string.Empty);
            _lastInputTime = Time.unscaledTime;
            gameObject.SetActive(true);

            LoadPasswordAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 입력을 비우고 창을 닫는다.
        /// </summary>
        public void Close()
        {
            _input.Clear();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 마지막 입력 뒤 제한 시간이 지나면 창을 닫는다 (창이 열려 있을 때만 실행됨).
        /// </summary>
        private void Update()
        {
            if (Time.unscaledTime - _lastInputTime >= idleTimeout)
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPasswordPanel] {idleTimeout}초 동안 입력이 없어 비밀번호 창을 닫습니다.");
                Close();
            }
        }

        /// <summary>
        /// Admin.json에서 비밀번호를 읽는다. 파일이 없거나 키패드로 입력할 수 없는 값이면 기본 비밀번호를 쓴다.
        /// </summary>
        private async UniTaskVoid LoadPasswordAsync(CancellationToken ct)
        {
            try
            {
                string path = ZString.Concat(Constants.ResourcePaths.SceneSettingsFolder, "/", Constants.Admin.SettingsFileName);
                AdminSettings settings = await JsonLoader.LoadAsync<AdminSettings>(path, ct, _logger);

                if (PasswordInput.IsValidPassword(settings.password))
                {
                    _password = settings.password;
                    return;
                }

                if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] Admin.json의 비밀번호가 숫자 {Constants.Admin.PasswordMinLength}~{Constants.Admin.PasswordMaxLength}자리가 아니어서 기본 비밀번호를 사용합니다.");
                _password = Constants.Admin.DefaultPassword;
            }
            catch (OperationCanceledException)
            {
                // 읽는 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 숫자 한 자리를 입력한다. 최대 자릿수를 넘는 입력은 무시한다.
        /// </summary>
        private void OnDigitClicked(int digit)
        {
            RegisterKeyPress();
            if (!_input.TryAppend(digit)) return;

            SetMessage(string.Empty);
            RefreshMasked();
        }

        /// <summary>
        /// 마지막 한 자리를 지운다.
        /// </summary>
        private void OnBackspaceClicked()
        {
            RegisterKeyPress();
            _input.RemoveLast();
            RefreshMasked();
        }

        /// <summary>
        /// 자릿수를 확인한 뒤 비밀번호가 맞으면 관리자 화면을 열고, 틀리면 안내를 띄우고 입력을 지운다.
        /// </summary>
        private void OnConfirmClicked()
        {
            RegisterKeyPress();

            if (!_input.HasValidLength)
            {
                SetMessage(Constants.Admin.PasswordLength);
                return;
            }

            if (!_input.Matches(_password))
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPasswordPanel] 관리자 비밀번호가 틀렸습니다.");
                _input.Clear();
                RefreshMasked();
                SetMessage(Constants.Admin.WrongPassword);
                return;
            }

            Close();
            if (adminPanel) adminPanel.Open();
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] adminPanel이 할당되지 않아 관리자 화면을 열 수 없습니다.");
        }

        /// <summary>
        /// 닫기 효과음을 내고 입력을 비운 채 창을 닫는다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            Close();
        }

        /// <summary>
        /// 키 입력 효과음을 내고 무입력 시간을 처음부터 다시 잰다.
        /// </summary>
        private void RegisterKeyPress()
        {
            _lastInputTime = Time.unscaledTime;
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
        }

        /// <summary>
        /// 0자리부터 최대 자릿수까지 ●를 이어 붙인 표시 문자열을 만든다.
        /// </summary>
        private static string[] CreateMaskTexts()
        {
            string[] texts = new string[Constants.Admin.PasswordMaxLength + 1];
            for (int i = 0; i < texts.Length; i++)
                texts[i] = new string('●', i);
            return texts;
        }

        /// <summary>
        /// 입력한 자릿수만큼 ●를 표시한다.
        /// </summary>
        private void RefreshMasked()
        {
            if (maskedText) maskedText.text = MaskTexts[_input.Length];
        }

        /// <summary>
        /// 안내 문구를 표시한다 (빈 문자열이면 지운다).
        /// </summary>
        private void SetMessage(string message)
        {
            if (messageText) messageText.text = message;
        }
    }
}
