using App;
using Cysharp.Threading.Tasks;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace Scenes
{
    public class IntroSceneManager : MonoBehaviour
    {
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private TutorialImageSlider tutorialSlider;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private TMP_Text visitorNameText;
        [Tooltip("화면 우상단 홈 버튼 — 이 버튼을 누른 터치는 튜토리얼로 넘기는 화면 터치로 받지 않는다")]
        [SerializeField] private HomeButton homeButton;

        private ILogger<IntroSceneManager> _logger;
        private VisitorInfoProvider _visitorInfoProvider;
        private InactivityTimer _inactivityTimer;
        private SoundManager _soundManager;

        // 이름 연출이 끝난 뒤부터 화면 터치로 튜토리얼 패널로 넘어갈 수 있다.
        // 연출을 스킵한 그 터치가 곧바로 패널 전환까지 일으키지 않도록 허용된 프레임은 건너뛴다.
        private bool _introTapEnabled;
        private int _introTapEnabledFrame;

        /// <summary>
        /// 로거, 체험자 정보 제공자, 비활동 타이머, 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<IntroSceneManager> log, VisitorInfoProvider visitorInfoProvider,
            InactivityTimer inactivityTimer, SoundManager soundManager)
        {
            _logger = log;
            _visitorInfoProvider = visitorInfoProvider;
            _inactivityTimer = inactivityTimer;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 패널 표시 상태를 정규화하고 튜토리얼 완료 구독·영상·나레이션 미리 읽기·체험자 이름 연출을 시작한다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[IntroSceneManager] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            if (!introPanel && _logger != null) _logger.ZLogWarning($"[IntroSceneManager] introPanel이 할당되지 않았습니다.");
            if (!tutorialPanel && _logger != null) _logger.ZLogWarning($"[IntroSceneManager] tutorialPanel이 할당되지 않았습니다.");
            if (!visitorNameText && _logger != null) _logger.ZLogWarning($"[IntroSceneManager] visitorNameText가 할당되지 않았습니다.");

            // UI 작업 중 에디터에서 패널을 꺼둔 채 플레이해도 항상 introPanel만 보이는 상태로 시작하도록 정규화
            SceneFader.InitializePanelState(introPanel, true);
            SceneFader.InitializePanelState(tutorialPanel, false);

            // 튜토리얼은 좌우 < > 버튼으로 넘기고, 마지막 페이지에서 > 버튼을 누르면 스토리 씬으로 간다
            if (tutorialSlider) tutorialSlider.Finished += OnTutorialFinished;
            else if (_logger != null) _logger.ZLogWarning($"[IntroSceneManager] tutorialSlider가 할당되지 않아 스토리 씬으로 넘어갈 수 없습니다.");

            SceneFader.PlayRobotVideo(robotVideoPlayer, destroyCancellationToken, _logger);

            // 앱을 켠 뒤 첫 재생도 줄 연출과 같은 프레임에 나오도록 나레이션 클립을 다 읽을 때까지 페이드인을 미룬다
            // (캐시에 있으면 바로 끝남, 읽기 실패는 SoundManager가 로그로 남긴다)
            if (_soundManager) SceneFader.RegisterPendingTask(_soundManager.PreloadAsync(Constants.Sounds.Narration, destroyCancellationToken));

            ApplyVisitorNameAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 텍스트의 "{name}" 플레이스홀더를 서버 연동 시 실제 이름, 미연동 시 VisitorSettings의 이름(관리자 페이지에서 변경)으로 치환한 뒤,
        /// 씬 페이드인이 끝나면 나레이션을 틀고 1_Intro.json의 시각에 맞춰 한 줄씩 올라오며 페이드인되는 연출로 표시한다.
        /// </summary>
        private async UniTaskVoid ApplyVisitorNameAsync(System.Threading.CancellationToken ct)
        {
            try
            {
                // visitorNameText 누락은 Start에서 이미 경고했다 — 연출 없이 화면 터치만 열어 준다
                if (!visitorNameText) return;

                // 페이드인을 기다리는 동안 원문이 보이지 않도록 먼저 숨긴다
                StoryLineAnimator.HideBeforeAnimate(visitorNameText);

                // 첫 문장이 페이드 커튼에 가려 지나가지 않도록 페이드인이 끝난 뒤 나레이션과 줄 연출을 같은 프레임에 시작한다
                IntroSceneSettings settings = await JsonLoader.LoadAsync<IntroSceneSettings>(Constants.SettingsFiles.Intro, ct, _logger);
                await UniTask.WaitWhile(() => SceneFader.IsLoading, cancellationToken: ct);

                if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.Narration);
                await StoryLineAnimator.AnimateWithVisitorNameAsync(visitorNameText, _visitorInfoProvider, ct, _inactivityTimer, _logger,
                    settings.narrationLineStartTimes);
            }
            catch (System.OperationCanceledException)
            {
                // 연출 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
            finally
            {
                // 연출이 끝나거나(스킵 포함), visitorNameText 미할당으로 애초에 연출이 없는 경우에도 화면 터치로 넘어갈 수 있어야 함
                _introTapEnabled = true;
                _introTapEnabledFrame = Time.frameCount;
            }
        }

        /// <summary>
        /// 이름 연출이 끝난 뒤 화면을 터치하면 나레이션을 멈추고 클릭음을 낸 뒤 튜토리얼 패널로 전환한다 (한 번만).
        /// </summary>
        private void Update()
        {
            if (!_introTapEnabled || Time.frameCount == _introTapEnabledFrame) return;
            if (!StoryLineAnimator.IsPointerPressedThisFrame()) return;

            // 홈 버튼을 누른 터치는 버튼이 타이틀로 보낸다 — 튜토리얼로 넘기기 시작하지 않는다
            if (homeButton && homeButton.ContainsScreenPoint(Pointer.current.position.ReadValue())) return;

            _introTapEnabled = false;
            // 연출을 스킵해 나레이션이 아직 나오는 중이면 튜토리얼 화면에 겹치지 않게 끊는다(클릭음보다 먼저 — 효과음 소스를 함께 쓴다)
            if (_soundManager)
            {
                _soundManager.StopSFX();
                _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            }
            SwitchToTutorialPanelAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 튜토리얼 완료 구독을 해제하고, 나레이션이 다음 씬까지 이어지지 않게 멈춘다.
        /// </summary>
        private void OnDestroy()
        {
            if (tutorialSlider) tutorialSlider.Finished -= OnTutorialFinished;
            // 나레이션 도중 디버그 키 등으로 씬을 떠나는 경우 — SoundManager는 씬이 바뀌어도 남는다
            if (_soundManager) _soundManager.StopSFX();
        }

        /// <summary>
        /// 크로스페이드(동시 진행) 대신 introPanel이 완전히 꺼진 뒤 tutorialPanel이 켜지는 순차 전환을 한다.
        /// </summary>
        private async UniTaskVoid SwitchToTutorialPanelAsync(System.Threading.CancellationToken ct)
        {
            try
            {
                SceneFader.SetGroupInteractable(introPanel, false);
                await SceneFader.FadeCanvasGroupAsync(introPanel, 1f, 0f, ct: ct);
                await SceneFader.FadeCanvasGroupAsync(tutorialPanel, 0f, 1f, ct: ct);
                SceneFader.SetGroupInteractable(tutorialPanel, true);
            }
            catch (System.OperationCanceledException)
            {
                // 전환 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 튜토리얼 마지막 페이지에서 다음으로 넘기면 스토리 씬으로 넘어간다 (연타는 SceneFader가 씬 전환 중이면 무시한다).
        /// </summary>
        private void OnTutorialFinished()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Story, logger: _logger).Forget();
        }
    }
}
