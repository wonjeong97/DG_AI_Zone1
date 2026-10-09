using System;
using System.Threading;
using App;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace Admin
{
    // 비밀번호를 통과하면 열리는 관리자 화면 — 운영 모드(로컬/서버)·체험자 이름·비밀번호를 바꾸고, 고른 레벨의 스토리 화면으로 바로 이동한다.
    public class AdminPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [Header("운영 모드")]
        [SerializeField] private Button localModeButton;
        [SerializeField] private Button serverModeButton;
        [Tooltip("지금 모드 버튼의 배경색")]
        [SerializeField] private Color selectedModeColor = new(0.55f, 0.75f, 1f, 1f);
        [Tooltip("지금 모드가 아닌 버튼의 배경색")]
        [SerializeField] private Color normalModeColor = new(0.82f, 0.82f, 0.82f, 1f);

        [Header("체험자 이름")]
        [SerializeField] private TMP_Text visitorNameText;
        [SerializeField] private Button changeNameButton;
        [SerializeField] private VisitorNamePanel namePanel;

        [Header("비밀번호")]
        [SerializeField] private Button changePasswordButton;
        [SerializeField] private AdminPasswordPanel passwordPanel;

        [Header("레벨 이동")]
        [Tooltip("레벨1부터 순서대로 — 누르면 그 레벨의 스토리 화면으로 바로 이동한다")]
        [SerializeField] private Button[] levelButtons;

        [Tooltip("변경 결과 안내 문구")]
        [SerializeField] private TMP_Text statusText;

        // 이 시간(초) 동안 입력이 없으면 관리자 화면을 닫는다 — 열 때마다 Admin.json(idleCloseSeconds)에서 다시 읽는다
        private float _idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds;

        private readonly IdleCloseTimer _idleTimer = new();
        private UnityAction[] _levelActions;

        // 관리자 화면을 열 때의 모드 — 닫을 때 달라졌으면 타이틀을 다시 불러 안내(QR·시작하기)에 반영한다
        private bool _modeAtOpen;

        // 레벨 이동·타이틀 다시 불러오기로 씬을 떠나는 중이면 닫기·레벨 버튼 입력을 무시한다
        // (SceneFader는 두 번째 전환을 무시하지만, 그 전에 다른 레벨 값이 세션에 들어가 엉뚱한 레벨 스토리가 열릴 수 있다)
        private bool _isLeaving;

        private ILogger<AdminPanel> _logger;
        private SoundManager _soundManager;
        private VisitorSettings _visitorSettings;
        private GameSession _session;
        private VisitorInfoProvider _visitorInfoProvider;
        private AdminScreenState _screenState;

        /// <summary>
        /// 로거, 사운드 매니저, 체험자 설정, 게임 세션, 체험자 정보 제공자, 관리자 창 상태를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<AdminPanel> logger, SoundManager soundManager, VisitorSettings visitorSettings, GameSession session,
            VisitorInfoProvider visitorInfoProvider, AdminScreenState screenState)
        {
            _logger = logger;
            _soundManager = soundManager;
            _visitorSettings = visitorSettings;
            _session = session;
            _visitorInfoProvider = visitorInfoProvider;
            _screenState = screenState;
        }

        /// <summary>
        /// 관리자 화면이 켜졌음을 알린다 — 열려 있는 동안 타이틀은 찍힌 QR을 서버로 보내지 않는다.
        /// </summary>
        private void OnEnable()
        {
            if (_screenState != null) _screenState.SetOpen(this, true);
        }

        /// <summary>
        /// 관리자 화면이 꺼졌음을 알린다.
        /// </summary>
        private void OnDisable()
        {
            if (_screenState != null) _screenState.SetOpen(this, false);
        }

        /// <summary>
        /// 버튼에 동작을 연결한다 (패널이 처음 켜질 때 한 번 실행).
        /// </summary>
        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] closeButton이 할당되지 않아 관리자 화면을 닫을 수 없습니다.");

            if (localModeButton) localModeButton.onClick.AddListener(OnLocalModeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] localModeButton이 할당되지 않았습니다.");

            if (serverModeButton) serverModeButton.onClick.AddListener(OnServerModeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] serverModeButton이 할당되지 않았습니다.");

            if (changeNameButton) changeNameButton.onClick.AddListener(OnChangeNameClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] changeNameButton이 할당되지 않았습니다.");

            if (changePasswordButton) changePasswordButton.onClick.AddListener(OnChangePasswordClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] changePasswordButton이 할당되지 않았습니다.");

            _levelActions = IndexedButtons.Bind(levelButtons, OnLevelClicked, index =>
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] 레벨{index + 1} 버튼이 할당되지 않았습니다.");
            });
        }

        /// <summary>
        /// 버튼 연결을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
            if (localModeButton) localModeButton.onClick.RemoveListener(OnLocalModeClicked);
            if (serverModeButton) serverModeButton.onClick.RemoveListener(OnServerModeClicked);
            if (changeNameButton) changeNameButton.onClick.RemoveListener(OnChangeNameClicked);
            if (changePasswordButton) changePasswordButton.onClick.RemoveListener(OnChangePasswordClicked);

            IndexedButtons.Unbind(levelButtons, _levelActions);
        }

        /// <summary>
        /// 지금 설정값을 보여 주며 관리자 화면을 연다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            _idleTimer.Restart();
            LoadIdleCloseSecondsAsync(destroyCancellationToken).Forget();
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 관리자 화면을 열었습니다.");
            if (!statusText && _logger != null) _logger.ZLogWarning($"[AdminPanel] statusText가 할당되지 않아 변경 결과를 표시하지 못합니다.");

            if (_visitorSettings) _modeAtOpen = _visitorSettings.IsServerConnected;
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] VisitorSettings가 주입되지 않아 모드·이름을 바꿀 수 없습니다.");

            RefreshMode();
            RefreshVisitorName();
            ShowStatus(string.Empty);
        }

        /// <summary>
        /// 현장에서 바뀌었을 수 있는 자동 닫기 시간을 Admin.json에서 다시 읽는다.
        /// </summary>
        private async UniTaskVoid LoadIdleCloseSecondsAsync(CancellationToken ct)
        {
            try
            {
                AdminSettings settings = await AdminSettings.LoadAsync(ct, _logger);
                _idleCloseSeconds = settings.idleCloseSeconds;
            }
            catch (OperationCanceledException)
            {
                // 읽는 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 변경 결과 안내 문구를 표시한다 (빈 문자열이면 지운다).
        /// </summary>
        public void ShowStatus(string message)
        {
            if (statusText) statusText.text = message;
        }

        /// <summary>
        /// 입력 없이 정해진 시간이 지나면 위에 떠 있는 이름 입력·비밀번호 변경 창까지 함께 닫는다 (화면이 열려 있을 때만 실행됨).
        /// </summary>
        private void Update()
        {
            if (_isLeaving || !_idleTimer.HasExpired(_idleCloseSeconds)) return;

            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] {_idleCloseSeconds}초 동안 입력이 없어 관리자 화면을 닫습니다.");
            if (namePanel) namePanel.Close();
            if (passwordPanel) passwordPanel.Close();
            Close();
        }

        /// <summary>
        /// 닫기 효과음을 내고 관리자 화면을 닫는다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (_isLeaving) return;

            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            Close();
        }

        /// <summary>
        /// 관리자 화면을 닫고, 모드가 바뀌었으면 타이틀을 다시 불러 반영한다.
        /// </summary>
        private void Close()
        {
            gameObject.SetActive(false);

            if (!_visitorSettings || _visitorSettings.IsServerConnected == _modeAtOpen) return;

            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 운영 모드가 바뀌어 타이틀을 다시 불러옵니다.");
            _isLeaving = true;
            SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
        }

        /// <summary>
        /// 로컬 모드(QR 없이 시작하기)로 바꾼다.
        /// </summary>
        private void OnLocalModeClicked()
        {
            SetServerConnected(false);
        }

        /// <summary>
        /// 서버 모드(QR 인식 후 시작하기)로 바꾼다.
        /// </summary>
        private void OnServerModeClicked()
        {
            SetServerConnected(true);
        }

        /// <summary>
        /// 운영 모드를 저장하고 버튼 표시와 안내를 갱신한다.
        /// </summary>
        private void SetServerConnected(bool isServerConnected)
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            if (!_visitorSettings || _visitorSettings.IsServerConnected == isServerConnected) return;

            _visitorSettings.IsServerConnected = isServerConnected;
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 운영 모드를 {(isServerConnected ? "서버" : "로컬")}로 바꿨습니다.");

            RefreshMode();
            ShowStatus(isServerConnected ? Constants.Admin.ServerModeSet : Constants.Admin.LocalModeSet);
        }

        /// <summary>
        /// 지금 모드 버튼만 강조색으로 칠한다.
        /// </summary>
        private void RefreshMode()
        {
            if (!_visitorSettings) return;

            bool isServerConnected = _visitorSettings.IsServerConnected;
            if (localModeButton && localModeButton.image) localModeButton.image.color = isServerConnected ? normalModeColor : selectedModeColor;
            if (serverModeButton && serverModeButton.image) serverModeButton.image.color = isServerConnected ? selectedModeColor : normalModeColor;
        }

        /// <summary>
        /// 지금 체험자 이름을 표시한다.
        /// </summary>
        private void RefreshVisitorName()
        {
            if (!visitorNameText)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] visitorNameText가 할당되지 않아 지금 체험자 이름을 표시하지 못했습니다.");
                return;
            }

            if (_visitorSettings) visitorNameText.text = _visitorSettings.VisitorName;
        }

        /// <summary>
        /// 이름 입력 창을 연다.
        /// </summary>
        private void OnChangeNameClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            ShowStatus(string.Empty);

            if (namePanel) namePanel.Open(OnVisitorNameSaved, _idleCloseSeconds);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] namePanel이 할당되지 않아 이름 입력 창을 열 수 없습니다.");
        }

        /// <summary>
        /// 이름 입력 창에서 저장한 이름을 체험자 설정에 저장한다.
        /// </summary>
        private void OnVisitorNameSaved(string visitorName)
        {
            if (!_visitorSettings)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] VisitorSettings가 주입되지 않아 체험자 이름을 저장하지 못했습니다.");
                return;
            }

            _visitorSettings.VisitorName = visitorName;
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 체험자 이름을 '{visitorName}'(으)로 바꿨습니다.");

            RefreshVisitorName();
            ShowStatus(Constants.Admin.VisitorNameChanged);
        }

        /// <summary>
        /// 비밀번호 창을 변경용으로 연다 — 저장 결과는 비밀번호 창이 ShowStatus로 알린다.
        /// </summary>
        private void OnChangePasswordClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            ShowStatus(string.Empty);

            if (passwordPanel) passwordPanel.OpenForChange();
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] passwordPanel이 할당되지 않아 비밀번호를 바꿀 수 없습니다.");
        }

        /// <summary>
        /// 고른 레벨까지 해금하고, 그 레벨을 고른 상태로 스토리 화면에 들어가도록 2_Story로 이동한다.
        /// </summary>
        private void OnLevelClicked(int index)
        {
            // 이 판은 스토리의 < 버튼이나 결과 화면의 다음 버튼으로 타이틀의 관리자 화면에 돌아온다.
            if (_isLeaving) return;

            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            if (_session == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] GameSession이 주입되지 않아 레벨로 이동할 수 없습니다.");
                return;
            }

            _session.unlockedLevelIndex = Mathf.Max(_session.unlockedLevelIndex, index);
            _session.pendingStoryLevelIndex = index;
            _session.isAdminLevelJump = true;

            // 관리자 시험 판이 타이틀에서 QR로 확인해 둔 체험자의 이름으로 행동 로그에 남지 않게 비운다
            if (_visitorInfoProvider != null) _visitorInfoProvider.ClearServerVisitor();
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] VisitorInfoProvider가 주입되지 않아 QR로 확인한 체험자를 비우지 못했습니다.");

            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 레벨{index + 1} 스토리 화면으로 이동합니다.");

            _isLeaving = true;
            SceneFader.FadeAndLoad(Constants.Scenes.Story, logger: _logger).Forget();
        }
    }
}
