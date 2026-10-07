using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
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

        // 0_Title.json 로드 전에 QR 확인 결과가 나오면 기본값을 쓴다
        private TitleSceneSettings _sceneSettings = new();

        // 무한 반복 깜빡임이라 씬을 떠날 때 직접 Kill한다
        private Tween _qrBlinkTween;

        // USB 바코드 스캐너는 키보드처럼 문자를 입력한 뒤 Enter를 보낸다 — Enter 전까지 모은 문자열이 QR 값.
        // 스캐너는 본체 키보드와 별개의 키보드 장치로 잡히므로 연결된 키보드 전부(나중에 꽂힌 것 포함)를 구독한다.
        private readonly StringBuilder _scanBuffer = new();
        private readonly List<Keyboard> _scanKeyboards = new();
        private bool _isWaitingForQr;

        /// <summary>
        /// 로거, 체험자 정보 제공자, 체험자 서버 API, 게임 세션, 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<TitleSceneManager> log, VisitorInfoProvider visitorInfoProvider,
            VisitorApiClient visitorApiClient, GameSession session, SoundManager soundManager)
        {
            _logger = log;
            _visitorInfoProvider = visitorInfoProvider;
            _visitorApiClient = visitorApiClient;
            _session = session;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 시작 버튼을 연결하고 서버 연동 여부에 따라 하단 안내(QR 인식 또는 시작하기)를 표시한다.
        /// </summary>
        private void Start()
        {
            if (!startButton && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] startButton이 할당되지 않았습니다.");
            if (!qrCanvasGroup && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] qrCanvasGroup이 할당되지 않았습니다.");
            if (!guideText && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] guideText가 할당되지 않았습니다.");

            if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);

            ApplyGuideAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 서버 연동(isServerConnected)이면 "QR 코드를 인식하여 주세요"를 띄우고 시작 버튼을 숨긴 채 QR 입력을 기다린다.
        /// 미연동이면 QR 단계 없이 "시작하기를 눌러주세요"와 시작 버튼을 바로 보여준다. 안내는 어느 쪽이든 천천히 깜빡인다.
        /// 페이드 시간은 StreamingAssets/Json/0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정한다.
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
                else ShowStartGuide();

                // qrCanvasGroup 누락은 Start에서 이미 경고했다
                if (!qrCanvasGroup) return;
                qrCanvasGroup.gameObject.SetActive(true);

                string settingsPath = ZString.Concat(Constants.ResourcePaths.SceneSettingsFolder, "/", Constants.Scenes.Title);
                _sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(settingsPath, ct, _logger);

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
        /// QR 안내를 띄우고 키보드(바코드 스캐너) 문자 입력을 받기 시작한다.
        /// </summary>
        private void WaitForQr()
        {
            if (guideText) guideText.text = Constants.TitleMessages.QrGuide;

            _scanBuffer.Clear();
            _isWaitingForQr = true;

            foreach (InputDevice device in InputSystem.devices)
                if (device is Keyboard keyboard) SubscribeScanKeyboard(keyboard);
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
        /// </summary>
        private void OnScanTextInput(char c)
        {
            if (!_isWaitingForQr) return;

            if (c == '\r' || c == '\n') SubmitScan();
            else if (!char.IsControl(c)) _scanBuffer.Append(c);
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
        /// </summary>
        private void SubmitScan()
        {
            string code = _scanBuffer.ToString();
            _scanBuffer.Clear();
            if (!_isWaitingForQr || string.IsNullOrWhiteSpace(code)) return;

            OnQrScanned(code);
        }

        /// <summary>
        /// QR 인식이 끝나면 입력 대기를 멈추고 서버에 체험자를 확인한다.
        /// </summary>
        private void OnQrScanned(string code)
        {
            StopWaitingForQr();
            if (_logger != null) _logger.ZLogInformation($"[TitleSceneManager] QR 인식 완료 (길이 {code.Length})");

            CheckVisitorAsync(code, destroyCancellationToken).Forget();
        }

        /// <summary>
        /// QR uid로 서버에 체험자를 확인한다. 확인되면 시작하기 안내로 바꾸고,
        /// 아니면(체험 완료·없는 QR·서버 오류) 이유를 잠시 보여 준 뒤 다시 QR을 기다린다.
        /// </summary>
        private async UniTaskVoid CheckVisitorAsync(string uid, CancellationToken ct)
        {
            if (guideText) guideText.text = Constants.TitleMessages.QrChecking;

            try
            {
                string failMessage = await ConfirmVisitorAsync(uid, ct);
                if (failMessage == null)
                {
                    ShowStartGuide();
                    return;
                }

                if (guideText) guideText.text = failMessage;

                // 0_Title.json에 음수를 적으면 Delay가 예외를 내 QR 대기로 돌아오지 못하므로 0 이상으로 제한한다
                float messageSeconds = Mathf.Max(0f, _sceneSettings.scanResultMessageSeconds);
                await UniTask.Delay(TimeSpan.FromSeconds(messageSeconds), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                WaitForQr();
            }
            catch (OperationCanceledException)
            {
                // 확인 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
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
            if (_session)
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
        /// 하단 안내를 "시작하기를 눌러주세요"로 바꾸고 시작 버튼을 보여준다.
        /// </summary>
        private void ShowStartGuide()
        {
            if (guideText) guideText.text = Constants.TitleMessages.StartGuide;
            if (startButton) startButton.gameObject.SetActive(true);
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

            // Start()에서 등록을 건너뛴 미할당 버튼도 있을 수 있으므로 해제도 동일하게 가드
            if (startButton) startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        /// <summary>
        /// 시작 버튼 클릭 시 게임 시작 효과음을 내고 인트로 씬으로 넘어간다.
        /// </summary>
        private void OnStartButtonClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.GameStart);
            SceneFader.FadeAndLoad(Constants.Scenes.Intro, logger: _logger).Forget();
        }
    }
}
