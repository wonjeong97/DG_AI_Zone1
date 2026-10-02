using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using ZLogger;

namespace Scenes
{
    public class IntroSceneManager : MonoBehaviour
    {
        private const string VisitorNamePlaceholder = "{name}";

        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private TutorialImageSlider tutorialSlider;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private TMP_Text visitorNameText;

        private ILogger<IntroSceneManager> _logger;
        private VisitorInfoProvider _visitorInfoProvider;
        private InactivityTimer _inactivityTimer;
        private SoundManager _soundManager;

        // 이름 연출이 끝난 뒤부터 화면 터치로 튜토리얼 패널로 넘어갈 수 있다.
        // 연출을 스킵한 그 터치가 곧바로 패널 전환까지 일으키지 않도록 허용된 프레임은 건너뛴다.
        private bool _introTapEnabled;
        private int _introTapEnabledFrame;
        private bool _isLoadingStory;

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
        /// 패널 표시 상태를 정규화하고 튜토리얼 완료 구독·영상·체험자 이름 연출을 시작한다.
        /// </summary>
        private void Start()
        {
            if (!introPanel && _logger != null) _logger.ZLogWarning($"[IntroSceneManager] introPanel이 할당되지 않았습니다.");
            if (!tutorialPanel && _logger != null) _logger.ZLogWarning($"[IntroSceneManager] tutorialPanel이 할당되지 않았습니다.");
            if (!visitorNameText && _logger != null) _logger.ZLogWarning($"[IntroSceneManager] visitorNameText가 할당되지 않았습니다.");

            // UI 작업 중 에디터에서 패널을 꺼둔 채 플레이해도 항상 introPanel만 보이는 상태로 시작하도록 정규화
            SceneFader.InitializePanelState(introPanel, true);
            SceneFader.InitializePanelState(tutorialPanel, false);

            // 튜토리얼은 이미지 좌/우 터치로 넘기고, 마지막 페이지에서 다음으로 넘기면 스토리 씬으로 간다
            if (tutorialSlider) tutorialSlider.Finished += OnTutorialFinished;
            else if (_logger != null) _logger.ZLogWarning($"[IntroSceneManager] tutorialSlider가 할당되지 않아 스토리 씬으로 넘어갈 수 없습니다.");

            SceneFader.PlayLoopingVideo(robotVideoPlayer, Constants.VideoPaths.RobotUrl, destroyCancellationToken, _logger);

            ApplyVisitorNameAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 텍스트의 "{name}" 플레이스홀더를 서버 연동 시 실제 이름, 미연동 시 Visitor.json의 기본 이름으로 치환한 뒤,
        /// 2_Story/5_Outro와 동일하게 한 줄씩 올라오며 페이드인되는 연출로 표시한다.
        /// </summary>
        private async UniTaskVoid ApplyVisitorNameAsync(System.Threading.CancellationToken ct)
        {
            try
            {
                // visitorNameText 누락은 Start에서 이미 경고했다 — 연출 없이 화면 터치만 열어 준다
                if (!visitorNameText) return;

                // 연출 시작 전까지 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
                visitorNameText.ForceMeshUpdate();
                visitorNameText.maxVisibleCharacters = 0;

                if (_visitorInfoProvider != null)
                {
                    string visitorName = await _visitorInfoProvider.GetNameAsync(ct);

                    using (Utf16ValueStringBuilder sb = ZString.CreateStringBuilder())
                    {
                        sb.Append(visitorNameText.text);
                        sb.Replace(VisitorNamePlaceholder, visitorName);
                        visitorNameText.text = sb.ToString();
                    }
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[IntroSceneManager] VisitorInfoProvider가 주입되지 않아 이름을 치환하지 않습니다.");
                }

                (float moveDuration, float interval, float yOffset) = await SceneFader.GetStoryLineSettingsAsync();
                await StoryLineAnimator.AnimateAsync(visitorNameText,
                    moveDuration, interval, yOffset,
                    StoryLineAnimator.IsPointerPressedThisFrame, ct, _inactivityTimer, _logger);
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
        /// 이름 연출이 끝난 뒤 화면을 터치하면 클릭음을 내고 튜토리얼 패널로 전환한다 (한 번만).
        /// </summary>
        private void Update()
        {
            if (!_introTapEnabled || Time.frameCount == _introTapEnabledFrame) return;
            if (!StoryLineAnimator.IsPointerPressedThisFrame()) return;

            _introTapEnabled = false;
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            SwitchToTutorialPanelAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 튜토리얼 완료 구독을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (tutorialSlider) tutorialSlider.Finished -= OnTutorialFinished;
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
        /// 튜토리얼 마지막 페이지에서 다음으로 넘기면 스토리 씬으로 넘어간다 (연타로 중복 로드되지 않게 한 번만).
        /// </summary>
        private void OnTutorialFinished()
        {
            if (_isLoadingStory) return;
            _isLoadingStory = true;
            SceneFader.FadeAndLoad(Constants.Scenes.Story, logger: _logger).Forget();
        }
    }
}
