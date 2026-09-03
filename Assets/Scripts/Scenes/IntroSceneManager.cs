using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Extensions.Logging;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace Scenes
{
    public class IntroSceneManager : MonoBehaviour
    {
        private const string VisitorNamePlaceholder = "{name}";

        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private Button introStartButton;
        [SerializeField] private Button tutorialStartButton;
        [SerializeField] private VideoPlayer robotVideoPlayer;
        [SerializeField] private TMP_Text visitorNameText;

        private ILogger<IntroSceneManager> _log;
        private VisitorInfoProvider _visitorInfoProvider;

        [Inject]
        public void Construct(ILogger<IntroSceneManager> log, VisitorInfoProvider visitorInfoProvider)
        {
            _log = log;
            _visitorInfoProvider = visitorInfoProvider;
        }

        private void Start()
        {
            if (!introPanel) _log?.ZLogWarning($"[IntroSceneManager] introPanel이 할당되지 않았습니다.");
            if (!tutorialPanel) _log?.ZLogWarning($"[IntroSceneManager] tutorialPanel이 할당되지 않았습니다.");
            if (!introStartButton) _log?.ZLogWarning($"[IntroSceneManager] introStartButton이 할당되지 않았습니다.");
            if (!tutorialStartButton) _log?.ZLogWarning($"[IntroSceneManager] tutorialStartButton이 할당되지 않았습니다.");
            if (!visitorNameText) _log?.ZLogWarning($"[IntroSceneManager] visitorNameText가 할당되지 않았습니다.");

            // UI 작업 중 에디터에서 패널을 꺼둔 채 플레이해도 항상 introPanel만 보이는 상태로 시작하도록 정규화
            SceneFader.InitializePanelState(introPanel, true);
            SceneFader.InitializePanelState(tutorialPanel, false);

            if (introStartButton)
            {
                introStartButton.onClick.AddListener(OnIntroStartClicked);
                // 이름 텍스트 연출이 끝나기 전까지는 시작 버튼을 눌러 넘어갈 수 없도록 비활성화
                introStartButton.interactable = false;
            }
            if (tutorialStartButton) tutorialStartButton.onClick.AddListener(OnTutorialStartClicked);

            SceneFader.PlayLoopingVideo(robotVideoPlayer, Constants.VideoPaths.RobotUrl, destroyCancellationToken);

            ApplyVisitorNameAsync(destroyCancellationToken).Forget();
        }

        // 텍스트의 "{name}" 플레이스홀더를 서버 연동 시 실제 이름, 미연동 시 Visitor.json의 기본 이름으로 치환한 뒤,
        // 2_Story/5_Outro와 동일하게 한 줄씩 올라오며 페이드인되는 연출로 표시함
        private async UniTaskVoid ApplyVisitorNameAsync(System.Threading.CancellationToken ct)
        {
            try
            {
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

                (float moveDuration, float interval, float yOffset) = await SceneFader.GetStoryLineSettingsAsync();
                await StoryLineAnimator.AnimateAsync(visitorNameText,
                    moveDuration, interval, yOffset,
                    StoryLineAnimator.IsPointerPressedThisFrame, ct);
            }
            finally
            {
                // 연출이 끝나거나(스킵 포함), visitorNameText 미할당으로 애초에 연출이 없는 경우에도 시작 버튼은 눌러야 함
                if (introStartButton) introStartButton.interactable = true;
            }
        }

        private void OnDestroy()
        {
            // Start()에서 등록을 건너뛴 미할당 버튼도 있을 수 있으므로 해제도 동일하게 가드
            if (introStartButton) introStartButton.onClick.RemoveListener(OnIntroStartClicked);
            if (tutorialStartButton) tutorialStartButton.onClick.RemoveListener(OnTutorialStartClicked);
        }

        private void OnIntroStartClicked()
        {
            SwitchToTutorialPanelAsync(destroyCancellationToken).Forget();
        }

        // 크로스페이드(동시 진행) 대신 introPanel이 완전히 꺼진 뒤 tutorialPanel이 켜지는 순차 전환
        private async UniTaskVoid SwitchToTutorialPanelAsync(System.Threading.CancellationToken ct)
        {
            SceneFader.SetGroupInteractable(introPanel, false);
            await SceneFader.FadeCanvasGroupAsync(introPanel, 1f, 0f, ct: ct);
            await SceneFader.FadeCanvasGroupAsync(tutorialPanel, 0f, 1f, ct: ct);
            SceneFader.SetGroupInteractable(tutorialPanel, true);
        }

        private void OnTutorialStartClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Story, logger: _log).Forget();
        }
    }
}
