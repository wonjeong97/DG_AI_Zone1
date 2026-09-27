using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Utils;
using ZLogger;

namespace Scenes
{
    public class TitleSceneManager : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private CanvasGroup qrCanvasGroup;

        private ILogger<TitleSceneManager> _logger;
        private VisitorInfoProvider _visitorInfoProvider;

        // 무한 반복 깜빡임이라 씬을 떠날 때 직접 Kill한다
        private Tween _qrBlinkTween;

        /// <summary>
        /// 로거와 체험자 정보 제공자를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<TitleSceneManager> log, VisitorInfoProvider visitorInfoProvider)
        {
            _logger = log;
            _visitorInfoProvider = visitorInfoProvider;
        }

        /// <summary>
        /// 시작 버튼을 연결하고 서버 연동 여부에 따라 QR 안내를 표시한다.
        /// </summary>
        private void Start()
        {
            if (!startButton && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] startButton이 할당되지 않았습니다.");
            if (!qrCanvasGroup && _logger != null) _logger.ZLogWarning($"[TitleSceneManager] qrCanvasGroup이 할당되지 않았습니다.");

            if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);

            ApplyQrVisibilityAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 서버 연동(isServerConnected) 여부에 따라 QR 안내를 표시하고, 표시할 때만 천천히 깜빡이게 한다.
        /// 페이드 시간은 StreamingAssets/Json/0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정한다.
        /// </summary>
        private async UniTaskVoid ApplyQrVisibilityAsync(CancellationToken ct)
        {
            // qrCanvasGroup 누락은 Start에서 이미 경고했다
            if (!qrCanvasGroup) return;
            if (_visitorInfoProvider == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[TitleSceneManager] VisitorInfoProvider가 주입되지 않아 QR 표시 여부를 판단할 수 없습니다.");
                return;
            }

            // 서버 연동 여부를 비동기로 확인하는 동안 QR이 잠깐 보였다 꺼지는 플리커를 막기 위해 먼저 숨겨 둠
            qrCanvasGroup.gameObject.SetActive(false);

            try
            {
                bool isServerConnected = await _visitorInfoProvider.IsServerConnectedAsync(ct);
                qrCanvasGroup.gameObject.SetActive(isServerConnected);

                if (isServerConnected)
                {
                    string settingsPath = ZString.Concat(Constants.ResourcePaths.SceneSettingsFolder, "/", Constants.Scenes.Title);
                    TitleSceneSettings sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(settingsPath, ct, _logger);

                    _qrBlinkTween = qrCanvasGroup.DOFade(sceneSettings.qrBlinkMinAlpha, sceneSettings.qrFadeDuration)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetLink(qrCanvasGroup.gameObject);
                }
            }
            catch (OperationCanceledException)
            {
                // 확인 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// QR 깜빡임 트윈과 시작 버튼 리스너를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_qrBlinkTween != null && _qrBlinkTween.IsActive()) _qrBlinkTween.Kill();

            // Start()에서 등록을 건너뛴 미할당 버튼도 있을 수 있으므로 해제도 동일하게 가드
            if (startButton) startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        /// <summary>
        /// 시작 버튼 클릭 시 인트로 씬으로 넘어간다.
        /// </summary>
        private void OnStartButtonClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Intro, logger: _logger).Forget();
        }
    }
}
