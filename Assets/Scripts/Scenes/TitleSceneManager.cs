using App;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace Scenes
{
    public class TitleSceneManager : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private CanvasGroup qrCanvasGroup;

        private ILogger<TitleSceneManager> _log;
        private VisitorInfoProvider _visitorInfoProvider;

        [Inject]
        public void Construct(ILogger<TitleSceneManager> log, VisitorInfoProvider visitorInfoProvider)
        {
            _log = log;
            _visitorInfoProvider = visitorInfoProvider;
        }

        private void Start()
        {
            if (!startButton) _log?.ZLogWarning($"[TitleSceneManager] startButton이 할당되지 않았습니다.");
            if (!qrCanvasGroup) _log?.ZLogWarning($"[TitleSceneManager] qrCanvasGroup이 할당되지 않았습니다.");

            if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);

            ApplyQrVisibilityAsync(destroyCancellationToken).Forget();
        }

        // 서버 연동(isServerConnected) 여부에 따라 QR 안내를 표시하고, 표시할 때만 천천히 깜빡임.
        // 페이드 시간은 StreamingAssets/Json/0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정 가능
        private async UniTaskVoid ApplyQrVisibilityAsync(CancellationToken ct)
        {
            if (!qrCanvasGroup || _visitorInfoProvider == null) return;

            bool isServerConnected = await _visitorInfoProvider.IsServerConnectedAsync(ct);
            qrCanvasGroup.gameObject.SetActive(isServerConnected);

            if (isServerConnected)
            {
                string settingsPath = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Title}";
                TitleSceneSettings sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(settingsPath, ct);

                qrCanvasGroup.DOFade(sceneSettings.qrBlinkMinAlpha, sceneSettings.qrFadeDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(qrCanvasGroup.gameObject);
            }
        }

        private void OnDestroy()
        {
            // Start()에서 등록을 건너뛴 미할당 버튼도 있을 수 있으므로 해제도 동일하게 가드
            if (startButton) startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Intro, logger: _log).Forget();
        }
    }
}
