using System;
using System.Threading;
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
    // 관리자 화면의 비밀번호 변경도 같은 키패드로 받는다 — 새 비밀번호를 두 번 입력해 같으면 Admin.json에 저장한다.
    public class AdminPasswordPanel : MonoBehaviour
    {
        [Tooltip("숫자 키 0~9 — 배열 인덱스가 그 키가 입력하는 숫자다")]
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backspaceButton;
        [SerializeField] private Button closeButton;

        [Tooltip("키패드 위 안내 문구 — 비밀번호 확인·새 비밀번호·한 번 더 입력 단계마다 바뀐다")]
        [SerializeField] private TMP_Text promptText;
        [Tooltip("입력한 자릿수만큼 ●를 표시하는 텍스트")]
        [SerializeField] private TMP_Text maskedText;
        [Tooltip("자릿수 부족·비밀번호 오류 안내 텍스트")]
        [SerializeField] private TMP_Text messageText;

        [SerializeField] private AdminPanel adminPanel;

        // 이 시간(초) 동안 키패드 입력이 없으면 창을 닫는다 — 열 때마다 Admin.json(passwordIdleCloseSeconds)에서 다시 읽는다
        private float _idleCloseSeconds = Constants.Admin.DefaultPasswordIdleCloseSeconds;

        // 지금 받는 입력 — 관리자 진입 비밀번호 확인, 또는 비밀번호 변경의 새 비밀번호·한 번 더 입력
        private enum Step
        {
            Verify,
            EnterNew,
            ConfirmNew
        }

        // 자릿수별 표시 문자열 — 키를 누를 때마다 문자열을 새로 만들지 않도록 미리 만들어 둔다
        private readonly static string[] MaskTexts = CreateMaskTexts();

        private readonly PasswordInput _input = new();
        private readonly IdleCloseTimer _idleTimer = new();
        private UnityAction[] _digitActions;
        private string _password = Constants.Admin.DefaultPassword;

        // 창을 열 때 Admin.json을 다시 읽는 중에는 확인을 받지 않는다 — 읽기 전 기본 비밀번호가 통과하지 않도록
        private bool _isPasswordLoaded;
        private Step _step;
        private string _newPassword;

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
            _digitActions = IndexedButtons.Bind(digitButtons, OnDigitClicked, digit =>
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] 숫자 {digit} 버튼이 할당되지 않았습니다.");
            });

            if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] confirmButton이 할당되지 않았습니다.");

            if (backspaceButton) backspaceButton.onClick.AddListener(OnBackspaceClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] backspaceButton이 할당되지 않았습니다.");

            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] closeButton이 할당되지 않았습니다.");

            if (!maskedText && _logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] maskedText가 할당되지 않아 입력한 자릿수를 표시하지 못합니다.");
            if (!messageText && _logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] messageText가 할당되지 않아 안내 문구를 표시하지 못합니다.");
        }

        /// <summary>
        /// 키패드 버튼 연결을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            IndexedButtons.Unbind(digitButtons, _digitActions);

            if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (backspaceButton) backspaceButton.onClick.RemoveListener(OnBackspaceClicked);
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>
        /// 관리자 진입용으로 창을 열고, 현장에서 바뀌었을 수 있는 비밀번호·자동 닫기 시간을 파일에서 다시 읽는다.
        /// </summary>
        public void Open()
        {
            _isPasswordLoaded = false;
            OpenAt(Step.Verify);
            LoadSettingsAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 비밀번호 변경용으로 창을 열어 새 비밀번호를 두 번 입력받는다 (관리자 화면 위에 뜬다).
        /// </summary>
        public void OpenForChange()
        {
            // 관리자 레벨 이동에서 돌아와 비밀번호 확인 없이 열린 경우도 있으므로 자동 닫기 시간을 다시 읽는다
            OpenAt(Step.EnterNew);
            LoadSettingsAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 입력을 비우고 창을 닫는다.
        /// </summary>
        public void Close()
        {
            _input.Clear();
            _newPassword = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 입력을 비운 채 주어진 단계로 창을 연다.
        /// </summary>
        private void OpenAt(Step step)
        {
            _newPassword = null;
            ShowStep(step);
            _idleTimer.Restart();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 마지막 입력 뒤 제한 시간이 지나면 창을 닫는다 (창이 열려 있을 때만 실행됨).
        /// </summary>
        private void Update()
        {
            if (_idleTimer.HasExpired(_idleCloseSeconds))
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPasswordPanel] {_idleCloseSeconds}초 동안 입력이 없어 비밀번호 창을 닫습니다.");
                Close();
            }
        }

        /// <summary>
        /// Admin.json에서 비밀번호와 자동 닫기 시간을 읽는다 (키패드로 입력할 수 없는 비밀번호면 기본 비밀번호를 쓴다).
        /// </summary>
        private async UniTaskVoid LoadSettingsAsync(CancellationToken ct)
        {
            try
            {
                AdminSettings settings = await AdminSettings.LoadAsync(ct, _logger);
                _idleCloseSeconds = settings.passwordIdleCloseSeconds;

                if (PasswordInput.IsValidPassword(settings.password))
                {
                    _password = settings.password;
                }
                else
                {
                    if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] Admin.json의 비밀번호가 숫자 {Constants.Admin.PasswordMinLength}~{Constants.Admin.PasswordMaxLength}자리가 아니어서 기본 비밀번호를 사용합니다.");
                    _password = Constants.Admin.DefaultPassword;
                }

                _isPasswordLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // 읽는 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 숫자 한 자리를 입력하고, 최대 자릿수를 넘는 입력은 무시한다.
        /// </summary>
        private void OnDigitClicked(int digit)
        {
            PlayKeySound();
            if (!_input.TryAppend(digit)) return;

            SetMessage(string.Empty);
            RefreshMasked();
        }

        /// <summary>
        /// 마지막 한 자리를 지운다.
        /// </summary>
        private void OnBackspaceClicked()
        {
            PlayKeySound();
            _input.RemoveLast();
            RefreshMasked();
        }

        /// <summary>
        /// 자릿수를 확인한 뒤 지금 단계에 맞게 처리한다 — 진입 확인, 새 비밀번호 받기, 한 번 더 입력한 값 비교.
        /// </summary>
        private void OnConfirmClicked()
        {
            PlayKeySound();

            if (!_input.HasValidLength)
            {
                SetMessage(Constants.Admin.PasswordLength);
                return;
            }

            switch (_step)
            {
                case Step.Verify:
                    ConfirmVerify();
                    break;

                case Step.EnterNew:
                    _newPassword = _input.ToString();
                    ShowStep(Step.ConfirmNew);
                    break;

                case Step.ConfirmNew:
                    ConfirmNewPassword();
                    break;
            }
        }

        /// <summary>
        /// 비밀번호가 맞으면 관리자 화면을 열고, 틀리면 안내를 띄우고 입력을 지운다.
        /// </summary>
        private void ConfirmVerify()
        {
            if (!_isPasswordLoaded)
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPasswordPanel] 비밀번호를 아직 읽는 중이라 확인을 받지 않았습니다.");
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
        /// 한 번 더 입력한 값이 새 비밀번호와 같으면 창을 닫고 저장하며, 다르면 새 비밀번호부터 다시 받는다.
        /// </summary>
        private void ConfirmNewPassword()
        {
            if (!_input.Matches(_newPassword))
            {
                ShowStep(Step.EnterNew);
                SetMessage(Constants.Admin.PasswordMismatch);
                return;
            }

            string newPassword = _newPassword;
            Close();
            SavePasswordAsync(newPassword, destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 새 비밀번호를 Admin.json에 저장하고 결과를 관리자 화면에 알린다.
        /// </summary>
        private async UniTaskVoid SavePasswordAsync(string newPassword, CancellationToken ct)
        {
            try
            {
                // 같은 파일의 다른 값(자동 닫기 시간·진입 클릭 수)을 지키도록 파일을 읽어 비밀번호만 바꿔 저장한다.
                // 파일이 깨져 있으면 기본값으로 읽혀 그 값들이 사라지므로 저장하지 않는다
                if (!AdminSettings.TryReadForSave(out AdminSettings current))
                {
                    if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] Admin.json 형식이 올바르지 않아 새 비밀번호를 저장하지 않았습니다.");
                    if (adminPanel) adminPanel.ShowStatus(Constants.Admin.PasswordSaveFailed);
                    return;
                }

                current.password = newPassword;
                await JsonLoader.SaveAsync(Constants.SettingsFiles.Admin, current, ct, _logger);
                // JsonLoader.SaveAsync는 실패를 로그로만 남기므로, 다시 읽어 실제로 저장됐는지 확인한다
                AdminSettings saved = await JsonLoader.LoadAsync<AdminSettings>(Constants.SettingsFiles.Admin, ct, _logger);

                // 파일 입출력 뒤 관리자 화면 UI를 고치므로 메인 스레드로 돌아온다
                await UniTask.SwitchToMainThread(ct);

                bool isSaved = saved.password == newPassword;
                if (_logger != null)
                {
                    if (isSaved) _logger.ZLogInformation($"[AdminPasswordPanel] 관리자 비밀번호를 변경했습니다.");
                    else _logger.ZLogWarning($"[AdminPasswordPanel] 새 비밀번호를 Admin.json에 저장하지 못했습니다.");
                }

                if (adminPanel) adminPanel.ShowStatus(isSaved ? Constants.Admin.PasswordChanged : Constants.Admin.PasswordSaveFailed);
                else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] adminPanel이 할당되지 않아 비밀번호 저장 결과를 관리자 화면에 알리지 못했습니다.");
            }
            catch (OperationCanceledException)
            {
                // 저장 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
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
        /// 키 입력 효과음을 낸다 (무입력 시간은 IdleCloseTimer가 누르기 입력으로 다시 잰다).
        /// </summary>
        private void PlayKeySound()
        {
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
        /// 입력을 비우고 단계에 맞는 안내 문구를 띄운다.
        /// </summary>
        private void ShowStep(Step step)
        {
            _step = step;
            _input.Clear();
            RefreshMasked();
            SetMessage(string.Empty);

            if (!promptText)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] promptText가 할당되지 않아 입력 단계 안내를 표시하지 못했습니다.");
                return;
            }

            promptText.text = step switch
            {
                Step.EnterNew => Constants.Admin.PromptNew,
                Step.ConfirmNew => Constants.Admin.PromptConfirm,
                _ => Constants.Admin.PromptVerify
            };
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
