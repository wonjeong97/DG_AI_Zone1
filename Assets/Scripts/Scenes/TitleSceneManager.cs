using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Data;
using HuliacDev.Network;
using HuliacDev.UI;
using HuliacDev.Utils;
using Network;
using ZLogger;

namespace Scenes
{
    public class TitleSceneManager : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private CanvasGroup qrCanvasGroup;
        [SerializeField] private TMP_Text guideText;

        private ILogger<TitleSceneManager> _logger;
        private VisitorInfoProvider _visitorInfoProvider;
        private VisitorApiClient _visitorApiClient;
        private GameSession _session;
        private SoundManager _soundManager;
        private AppSettingsProvider _settingsProvider;
        private ApiManagerBase _apiManager;

        // 0_Title.json 로드 전에 QR 확인 결과가 나오면 기본값을 쓴다
        private TitleSceneSettings _sceneSettings = new();

        // QR로 확인한 체험자가 시작하기를 누르지 않고 기다린 시간 재기 — 시작하기·새 QR·씬 파괴 때 취소한다
        private CancellationTokenSource _confirmTimeoutCts;

        // 무한 반복 깜빡임이라 씬을 떠날 때 직접 Kill한다
        private Tween _qrBlinkTween;

        // USB 바코드 스캐너는 키보드처럼 문자를 입력한 뒤 Enter를 보낸다 — Enter 전까지 모은 문자열이 QR 값.
        // 글자 사이가 0_Title.json scanCharGapSeconds(기본 0.5초)보다 벌어지면 앞에 모은 글자(찍기 전에 눌린 키 등)는 버린다(ScanInputBuffer).
        // 스캐너는 본체 키보드와 별개의 키보드 장치로 잡히므로 연결된 키보드 전부(나중에 꽂힌 것 포함)를 구독한다.
        private readonly ScanInputBuffer _scanBuffer = new();
        private readonly List<Keyboard> _scanKeyboards = new();
        private bool _isWaitingForQr;

        /// <summary>
        /// 로거, 체험자 정보 제공자, 체험자 서버 API, 게임 세션, 사운드 매니저, 앱 설정(Settings.json) 제공자, 콘텐츠 로그 API 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<TitleSceneManager> log, VisitorInfoProvider visitorInfoProvider,
            VisitorApiClient visitorApiClient, GameSession session, SoundManager soundManager, AppSettingsProvider settingsProvider,
            ApiManagerBase apiManager)
        {
            _logger = log;
            _visitorInfoProvider = visitorInfoProvider;
            _visitorApiClient = visitorApiClient;
            _session = session;
            _soundManager = soundManager;
            _settingsProvider = settingsProvider;
            _apiManager = apiManager;
        }

        /// <summary>
        /// 시작 버튼을 연결하고 서버 연동 여부에 따라 하단 안내(QR 인식 또는 시작하기)를 표시한다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[TitleSceneManager] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            if (!startButton && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] startButton이 할당되지 않았습니다.");
            if (!qrCanvasGroup && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] qrCanvasGroup이 할당되지 않았습니다.");
            if (!guideText && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] guideText가 할당되지 않았습니다.");

            if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);

            ApplyGuideAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 서버 연동(isServerConnected)이면 "QR 코드를 인식하여 주세요"를 띄우고 시작 버튼을 숨긴 채 QR 입력을 기다린다.
        /// 미연동이면 QR 단계 없이 "시작하기를 눌러주세요"와 시작 버튼을 바로 보여준다. 안내는 어느 쪽이든 천천히 깜빡인다.
        /// 페이드 시간·QR 확인 시간·스캐너 글자 간격은 StreamingAssets/Json/0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정한다.
        /// </summary>
        private async UniTaskVoid ApplyGuideAsync(CancellationToken ct)
        {
            // 서버 모드면 QR 인식 전까지 시작 버튼이 보이면 안 되므로 먼저 숨겨 둠
            if (startButton) startButton.gameObject.SetActive(false);

            try
            {
                bool isServerConnected = false;
                if (_visitorInfoProvider != null)
                    isServerConnected = _visitorInfoProvider.IsServerConnected;
                else if (_logger != null)
                    _logger.ZLogWarning($"[TitleSceneManager] VisitorInfoProvider가 주입되지 않아 서버 미연동으로 보고 시작하기 안내를 표시합니다.");

                if (isServerConnected) WaitForQr();
                else ShowStartGuide(Constants.TitleMessages.StartGuide);

                // qrCanvasGroup 누락은 Start에서 이미 경고했다
                if (qrCanvasGroup) qrCanvasGroup.gameObject.SetActive(true);

                // 안내가 없어도 QR 확인·스캐너 값은 써야 하므로 설정은 항상 읽는다
                _sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(Constants.SettingsFiles.Title, ct, _logger);
                ApplyScanCharGap();

                if (!qrCanvasGroup) return;

                _qrBlinkTween = qrCanvasGroup.DOFade(_sceneSettings.qrBlinkMinAlpha, _sceneSettings.qrFadeDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(qrCanvasGroup.gameObject);
            }
            catch (OperationCanceledException)
            {
                // 확인 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 0_Title.json의 스캐너 글자 사이 최대 간격을 적용한다. 0 이하면 경고를 남기고 기본값을 쓴다.
        /// </summary>
        private void ApplyScanCharGap()
        {
            float gap = _sceneSettings.scanCharGapSeconds;
            if (gap > 0f)
            {
                _scanBuffer.MaxCharGapSeconds = gap;
                return;
            }

            _scanBuffer.MaxCharGapSeconds = ScanInputBuffer.DefaultMaxCharGapSeconds;
            if (_logger != null) _logger.ZLogWarning($"[TitleSceneManager] 0_Title.json의 scanCharGapSeconds({gap})가 0 이하라 기본값 {ScanInputBuffer.DefaultMaxCharGapSeconds}초를 씁니다.");
        }

        /// <summary>
        /// 시작 버튼을 숨기고 QR 안내를 띄운 뒤 키보드(바코드 스캐너) 문자 입력을 받기 시작한다.
        /// </summary>
        private void WaitForQr()
        {
            if (startButton) startButton.gameObject.SetActive(false);
            if (guideText) guideText.text = Constants.TitleMessages.QrGuide;
            StartScanning();
        }

        /// <summary>
        /// 키보드(바코드 스캐너) 문자 입력을 받기 시작한다.
        /// 이미 받고 있으면(시작하기 안내 중) 모으던 글자를 지우지 않는다 — 시작하기 대기 시간이 끝나는 순간 들어오던 스캔의
        /// 앞 글자가 잘려 '등록되지 않은 QR'이 되지 않게 한다. 오래 머문 글자는 ScanInputBuffer가 글자 간격으로 버린다.
        /// </summary>
        private void StartScanning()
        {
            if (!_isWaitingForQr) _scanBuffer.Clear();
            _isWaitingForQr = true;

            foreach (InputDevice device in InputSystem.devices)
                if (device is Keyboard keyboard) SubscribeScanKeyboard(keyboard);

            // 이미 받고 있는 중에 다시 불려도 장치 연결 이벤트가 두 번 걸리지 않게 뺐다가 건다
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;

            if (_scanKeyboards.Count == 0 && _logger != null)
                _logger.ZLogWarning($"[TitleSceneManager] 연결된 키보드(바코드 스캐너)가 없습니다. 장치가 연결되면 QR 입력을 받기 시작합니다.");
        }

        /// <summary>
        /// QR 대기 중 연결·재연결된 키보드(스캐너)는 입력을 받도록 구독하고, 빠진 장치는 목록에서 뺀다.
        /// </summary>
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Keyboard keyboard) return;

            switch (change)
            {
                // 스캐너를 다시 꽂으면 Input System은 같은 장치를 Added가 아니라 Reconnected로 알린다
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    SubscribeScanKeyboard(keyboard);
                    break;

                // 스캐너 케이블이 빠지면 제거된 장치의 키 상태를 매 프레임 읽지 않도록 목록에서 뺀다
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    UnsubscribeScanKeyboard(keyboard);
                    break;
            }
        }

        /// <summary>
        /// 키보드 하나의 문자 입력을 구독한다 (중복 구독 방지).
        /// </summary>
        private void SubscribeScanKeyboard(Keyboard keyboard)
        {
            if (_scanKeyboards.Contains(keyboard)) return;
            keyboard.onTextInput += OnScanTextInput;
            _scanKeyboards.Add(keyboard);
        }

        /// <summary>
        /// 키보드 하나의 문자 입력 구독을 해제한다 (구독하지 않은 장치면 무시).
        /// </summary>
        private void UnsubscribeScanKeyboard(Keyboard keyboard)
        {
            if (!_scanKeyboards.Remove(keyboard)) return;
            keyboard.onTextInput -= OnScanTextInput;
        }

        /// <summary>
        /// 스캐너가 보낸 문자를 모은다. 스캐너가 Enter를 CR/LF 문자로 보내는 경우 그 자리에서 인식을 끝낸다.
        /// 앞 글자와 scanCharGapSeconds(기본 0.5초)보다 벌어진 글자가 오면 앞에 모은 글자는 이번 스캔이 아니라 버린다
        /// (uid에 생년월일이 있어 글자 내용 대신 개수만 로그에 남긴다).
        /// </summary>
        private void OnScanTextInput(char c)
        {
            if (!_isWaitingForQr) return;

            if (c == '\r' || c == '\n')
            {
                SubmitScan();
                return;
            }

            // Tab 등 제어 문자는 QR 값이 아니다
            if (char.IsControl(c)) return;

            int discarded = _scanBuffer.Append(c, Time.realtimeSinceStartup);
            if (discarded > 0 && _logger != null)
                _logger.ZLogInformation($"[TitleSceneManager] 글자 사이가 {_scanBuffer.MaxCharGapSeconds}초 넘게 벌어져 앞에 모은 {discarded}글자를 버리고 새로 모읍니다.");
        }

        /// <summary>
        /// Enter가 문자로 오지 않는 장치를 위해, QR 대기 중 어느 키보드든 Enter 키가 눌리면 인식을 끝낸다.
        /// </summary>
        private void Update()
        {
            if (!_isWaitingForQr) return;

            foreach (Keyboard keyboard in _scanKeyboards)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    SubmitScan();
                    return;
                }
            }
        }

        /// <summary>
        /// 모은 문자열을 QR 값으로 처리한다 — 비어 있으면(Enter만 들어온 경우) 무시하고 계속 기다린다.
        /// 마지막 글자 뒤로 scanCharGapSeconds(기본 0.5초)보다 늦게 온 Enter면 모은 글자는 스캔이 아니라 손으로 누른 키로 보고 버린다.
        /// </summary>
        private void SubmitScan()
        {
            bool isStale = _scanBuffer.IsStale(Time.realtimeSinceStartup);
            string code = _scanBuffer.TakeAndClear();
            if (!_isWaitingForQr || string.IsNullOrWhiteSpace(code)) return;

            if (isStale)
            {
                if (_logger != null) _logger.ZLogInformation($"[TitleSceneManager] 마지막 글자보다 {_scanBuffer.MaxCharGapSeconds}초 넘게 늦게 Enter가 와서 모은 {code.Length}글자를 QR로 보지 않고 버립니다.");
                return;
            }

            OnQrScanned(code);
        }

        /// <summary>
        /// QR 인식이 끝나면 입력 대기를 멈추고 서버에 체험자를 확인한다.
        /// 시작하기가 떠 있는 동안 다음 사람이 찍은 경우에도 앞사람 기록을 비우고 새로 확인한다.
        /// </summary>
        private void OnQrScanned(string code)
        {
            StopWaitingForQr();
            CancelConfirmTimeout();
            if (startButton) startButton.gameObject.SetActive(false);
            ClearConfirmedVisitor();

            CheckVisitorAsync(code, destroyCancellationToken).Forget();
        }

        /// <summary>
        /// QR uid로 서버에 체험자를 확인한다. '확인하고 있습니다'를 최소 시간만큼은 보여 준 뒤, 확인되면 시작하기 안내로 바꾸고
        /// 아니면(체험 완료·없는 QR·서버 오류) 이유를 잠시 보여 준 뒤 다시 QR을 기다린다.
        /// </summary>
        private async UniTaskVoid CheckVisitorAsync(string uid, CancellationToken ct)
        {
            if (guideText) guideText.text = Constants.TitleMessages.QrChecking;
            float checkStartTime = Time.realtimeSinceStartup;

            try
            {
                string failMessage = await ConfirmVisitorAsync(uid, ct);

                // 서버가 빨리 답해도 '확인하고 있습니다'가 스치듯 지나가지 않게 최소 시간을 채운다 — 이미 지났으면 바로 넘어간다
                // 0_Title.json을 다 읽기 전에 찍었으면 최소 시간 없이 바로 결과를 보여 준다
                float minSeconds = _sceneSettings != null ? _sceneSettings.qrCheckingMinSeconds : 0f;
                float remainingSeconds = minSeconds - (Time.realtimeSinceStartup - checkStartTime);
                if (remainingSeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(remainingSeconds), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                if (failMessage == null)
                {
                    if (_logger != null) _logger.ZLogInformation($"[TitleSceneManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} QR을 찍음 — 체험할 수 있음.");
                    ShowConfirmedVisitor();
                    return;
                }

                await ShowScanFailureAsync(failMessage, ct);
            }
            catch (OperationCanceledException)
            {
                // 확인 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
            catch (Exception ex)
            {
                // 예상하지 못한 오류로 '확인하고 있습니다'에 멈추지 않게 한다 — 타이틀은 비활동 타이머도 멈춰 있어 앱을 다시 켜야 하게 된다
                if (_logger != null) _logger.ZLogError($"[TitleSceneManager] 체험자 확인 중 오류가 나 QR 대기로 돌아갑니다: {ex}");
                ClearConfirmedVisitor();
                ShowScanFailureThenForget(Constants.TitleMessages.QrCheckFailed, ct);
            }
        }

        /// <summary>
        /// QR 확인 실패 이유를 잠시 보여 준 뒤 다시 QR을 기다린다.
        /// </summary>
        private async UniTask ShowScanFailureAsync(string message, CancellationToken ct)
        {
            if (guideText) guideText.text = message;

            // 0_Title.json에 음수를 적으면 Delay가 예외를 내 QR 대기로 돌아오지 못하므로 0 이상으로 제한한다
            float messageSeconds = _sceneSettings != null ? Mathf.Max(0f, _sceneSettings.scanResultMessageSeconds) : 0f;
            await UniTask.Delay(TimeSpan.FromSeconds(messageSeconds), DelayType.UnscaledDeltaTime, cancellationToken: ct);
            WaitForQr();
        }

        /// <summary>
        /// catch 블록에서 실패 안내를 이어서 보여 준다 — 씬을 떠나 취소되면 조용히 끝낸다.
        /// </summary>
        private void ShowScanFailureThenForget(string message, CancellationToken ct)
        {
            ShowScanFailureAsync(message, ct).SuppressCancellationThrow().Forget();
        }

        /// <summary>
        /// 서버에 체험 가능 여부(checkActive)와 진행도(getUser)를 물어 체험자와 해금 레벨을 기록한다.
        /// 체험할 수 없으면 하단에 보여 줄 안내 문구를, 확인되면 null을 돌려준다.
        /// </summary>
        private async UniTask<string> ConfirmVisitorAsync(string uid, CancellationToken ct)
        {
            if (_visitorApiClient == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleSceneManager] VisitorApiClient가 주입되지 않아 체험자를 확인할 수 없습니다.");
                return Constants.TitleMessages.QrCheckFailed;
            }

            CheckActiveResult active = await _visitorApiClient.CheckActiveAsync(uid, ct);
            if (active.Status != CheckActiveStatus.Active) return GetScanFailMessage(active.Status);

            GetUserResult progress = await _visitorApiClient.GetUserAsync(uid, ct);
            if (!progress.IsFound) return Constants.TitleMessages.QrCheckFailed;

            if (_visitorInfoProvider != null)
                _visitorInfoProvider.SetServerVisitor(active.IdxUser, active.Name);
            else if (_logger != null)
                _logger.ZLogWarning($"[TitleSceneManager] VisitorInfoProvider가 주입되지 않아 확인한 체험자를 기록하지 못했습니다.");

            // 성공·실패와 상관없이 기록이 있는 마지막 레벨의 다음 레벨까지 연다 — 로컬 진행 규칙(ResultSequence.OnNextClicked)과 같다
            if (_session != null)
                _session.unlockedLevelIndex = progress.LastRecordedLevelIndex + 1;
            else if (_logger != null)
                _logger.ZLogWarning($"[TitleSceneManager] GameSession이 주입되지 않아 서버 진행도를 반영하지 못했습니다.");

            return null;
        }

        /// <summary>
        /// 체험할 수 없는 QR 확인 결과를 하단 안내 문구로 바꾼다.
        /// </summary>
        private static string GetScanFailMessage(CheckActiveStatus status)
        {
            return status switch
            {
                CheckActiveStatus.Completed => Constants.TitleMessages.QrCompleted,
                CheckActiveStatus.NotFound  => Constants.TitleMessages.QrNotFound,
                _                           => Constants.TitleMessages.QrCheckFailed
            };
        }

        /// <summary>
        /// 하단 안내를 시작하기 안내 문구로 바꾸고 시작 버튼을 보여준다.
        /// </summary>
        private void ShowStartGuide(string message)
        {
            if (guideText) guideText.text = message;
            if (startButton) startButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// QR로 확인한 체험자에게 이름이 들어간 시작 안내와 시작 버튼을 보여 준다.
        /// 다음 사람이 QR을 찍을 수 있게 스캐너 입력을 계속 받고, 시작하기를 기다린 시간을 재기 시작한다.
        /// </summary>
        private void ShowConfirmedVisitor()
        {
            string visitorName = _visitorInfoProvider != null ? _visitorInfoProvider.GetName() : null;
            ShowStartGuide(string.IsNullOrEmpty(visitorName)
                ? Constants.TitleMessages.StartGuide
                : ZString.Format(Constants.TitleMessages.StartGuideWithNameFormat, visitorName));

            StartScanning();
            StartConfirmTimeout();
        }

        /// <summary>
        /// 확인했던 체험자와 서버 진행도로 연 해금 레벨을 비운다 — 다음 사람의 QR로 다시 확인하거나 대기 시간이 지났을 때.
        /// </summary>
        private void ClearConfirmedVisitor()
        {
            if (_visitorInfoProvider != null) _visitorInfoProvider.ClearServerVisitor();
            if (_session != null) _session.unlockedLevelIndex = 0;
        }

        /// <summary>
        /// 시작하기를 기다린 시간 재기를 새로 시작한다 — 이전에 재던 것은 취소한다.
        /// </summary>
        private void StartConfirmTimeout()
        {
            CancelConfirmTimeout();
            _confirmTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ConfirmTimeoutAsync(_confirmTimeoutCts).Forget();
        }

        /// <summary>
        /// 시작하기를 기다린 시간 재기를 취소한다 — 시작하기를 눌렀거나 새 QR이 들어왔거나 씬을 떠날 때.
        /// </summary>
        private void CancelConfirmTimeout()
        {
            if (_confirmTimeoutCts == null) return;

            _confirmTimeoutCts.Cancel();
            _confirmTimeoutCts.Dispose();
            _confirmTimeoutCts = null;
        }

        /// <summary>
        /// 비활동 타이머와 같은 설정(Settings.json의 useInactivityTimer·resetTime)으로, 시작하기를 누르지 않은 채
        /// 그 시간이 지나면 서버에 move_idle_timeout을 한 번 보내고 확인한 체험자를 비운 뒤 다시 QR을 기다린다. 비활동 타이머가 꺼져 있으면 계속 기다린다.
        /// 타이틀에서 난 비활동 타임아웃은 APIManager가 보내지 않으므로, 타이틀의 move_idle_timeout은 이 경우에만 남는다.
        /// </summary>
        private async UniTaskVoid ConfirmTimeoutAsync(CancellationTokenSource cts)
        {
            // 취소 시 CancelConfirmTimeout이 CTS를 바로 해제하므로 토큰을 먼저 받아 둔다
            CancellationToken ct = cts.Token;

            try
            {
                if (_settingsProvider == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[TitleSceneManager] AppSettingsProvider가 주입되지 않아 시작하기 대기 시간 제한 없이 기다립니다.");
                    return;
                }

                Settings settings = await _settingsProvider.GetAsync(ct);
                if (settings == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[TitleSceneManager] Settings.json을 읽지 못해 시작하기 대기 시간 제한 없이 기다립니다.");
                    return;
                }

                // 비활동 타이머를 끈 설정이면 시작하기도 시간 제한 없이 기다린다
                if (!settings.useInactivityTimer || settings.resetTime <= 0f) return;

                await UniTask.Delay(TimeSpan.FromSeconds(settings.resetTime), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                if (_logger != null) _logger.ZLogInformation($"[TitleSceneManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} {settings.resetTime}초 동안 시작하기를 누르지 않아 QR 대기로 돌아감.");

                // 로그 전송은 씬과 상관없이 끝까지 보내도록 이 오브젝트의 토큰을 넘기지 않는다
                if (_apiManager) _apiManager.SendMoveIdleTimeoutLogAsync().Forget();
                else if (_logger != null) _logger.ZLogWarning($"[TitleSceneManager] ApiManagerBase가 주입되지 않아 시작하기 대기 시간 초과 로그(move_idle_timeout)를 보내지 못했습니다.");

                ClearConfirmedVisitor();
                WaitForQr();
            }
            catch (OperationCanceledException)
            {
                // 시작하기·새 QR·씬 파괴로 취소된 정상 흐름
            }
            finally
            {
                // 다른 재기가 이미 새로 시작됐으면 그 CTS는 건드리지 않는다
                if (_confirmTimeoutCts == cts)
                {
                    _confirmTimeoutCts.Dispose();
                    _confirmTimeoutCts = null;
                }
            }
        }

        /// <summary>
        /// 스캐너 문자 입력·장치 연결 구독을 해제한다.
        /// </summary>
        private void StopWaitingForQr()
        {
            _isWaitingForQr = false;
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (Keyboard keyboard in _scanKeyboards) keyboard.onTextInput -= OnScanTextInput;
            _scanKeyboards.Clear();
        }

        /// <summary>
        /// QR 깜빡임 트윈, 스캐너 입력 구독, 시작 버튼 리스너를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_qrBlinkTween != null && _qrBlinkTween.IsActive()) _qrBlinkTween.Kill();
            StopWaitingForQr();
            CancelConfirmTimeout();

            // Start()에서 등록을 건너뛴 미할당 버튼도 있을 수 있으므로 해제도 동일하게 가드
            if (startButton) startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        /// <summary>
        /// 시작 버튼 클릭 시 게임 시작 효과음을 내고 인트로 씬으로 넘어간다.
        /// 넘어가는 페이드 동안 QR이 찍혀 체험자가 바뀌거나 대기 시간이 지나 QR 대기로 돌아가지 않게 둘 다 멈춘다.
        /// </summary>
        private void OnStartButtonClicked()
        {
            if (_logger != null) _logger.ZLogInformation($"[TitleSceneManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 시작하기를 누름.");
            StopWaitingForQr();
            CancelConfirmTimeout();
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.GameStart);
            SceneFader.FadeAndLoad(Constants.Scenes.Intro, logger: _logger).Forget();
        }
    }
}
