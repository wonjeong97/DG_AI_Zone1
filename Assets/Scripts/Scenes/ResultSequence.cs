using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VContainer;

namespace DG.Scenes
{
    public class ResultSequence : MonoBehaviour
    {
        [SerializeField] private TypewriterText playerText;
        [SerializeField] private CanvasGroup playerImageGroup;
        [SerializeField] private TypewriterText aiText;
        [SerializeField] private CanvasGroup aiImageGroup;
        [SerializeField] private float fadeDuration = 0.5f;

        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button nextButton;
        [SerializeField] private CanvasGroup confirmButtonGroup;
        [SerializeField] private Button confirmButton;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private CanvasGroup videoPanelGroup;

        [Inject] private GameSession _session;

        private void Start()
        {
            // CompletePanel은 크로스페이드 전까지 투명 상태 — 미리 입력을 막아 ResultPanel을 가리지 않도록
            SceneFader.SetGroupInteractable(completePanel, false);

            // 확인 버튼은 시퀀스에서 페이드인 완료 후에만 입력 가능
            confirmButtonGroup.alpha = 0f;
            SceneFader.SetGroupInteractable(confirmButtonGroup, false);

            if (videoPanelGroup)
            {
                videoPanelGroup.alpha = 0f;
                SceneFader.SetGroupInteractable(videoPanelGroup, false);
            }

            if (nextButton)
                nextButton.onClick.AddListener(OnNextClicked);

            if (videoPlayer)
            {
                videoPlayer.url = Path.Combine(Application.streamingAssetsPath, "Videos/11.mp4");
                videoPlayer.Prepare();
                SceneFader.RegisterPendingTask(UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: destroyCancellationToken));
            }

            ApplySessionResults();
            PlaySequence().Forget();
        }

        // 다음 레벨로 진행 — 방금 플레이한 레벨의 afterResultScene을 따라감 (마지막 레벨은 5_Outro)
        private void OnNextClicked()
        {
            string nextScene = _session && _session.currentLevel ? _session.currentLevel.afterResultScene : "2_Story";
            if (_session) _session.unlockedLevelIndex++;
            SceneFader.FadeAndLoad(nextScene).Forget();
        }

        // 4_Game에서 저장한 결과로 텍스트 구성 — 거치지 않고 진입하면 씬 기본 텍스트 유지
        private void ApplySessionResults()
        {
            if (!_session || _session.lastQuestionTime is null)
                return;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시
            bool hasCoding = _session.lastAngle is not null
                             && _session.lastCount is not null
                             && _session.lastDirection is not null;

            if (hasCoding)
            {
                // 최고 점수 대비 비율로 전력 수급 상태 판정
                int maxScore = BlockScorer.GetMaxScore();
                float percent = maxScore > 0 ? _session.lastScore * 100f / maxScore : 0f;
                string status = percent < 50f ? "부족" : percent < 80f ? "보통" : "양호";

                playerText.SetText(BuildResultText("[체험자 코딩]",
                    _session.lastAngle, _session.lastCount, _session.lastDirection, status));

                if (status == "부족")
                    ApplyGrayscale();
            }
            else
            {
                playerText.SetText("[체험자 코딩]\n\n-\n\n전력 수급 상태: -");
                ApplyGrayscale();
            }

            aiText.SetText(BuildResultText("[AI 코딩]",
                BlockScorer.GetBestAngle(),
                BlockScorer.GetBestCount(),
                BlockScorer.GetBestDirection(_session.lastQuestionTime),
                "양호"));
        }

        // 전력 부족 판정 — 체험자 이미지(Image_Object)를 흑백으로 표시
        private void ApplyGrayscale()
        {
            if (!playerImageGroup.TryGetComponent<Image>(out Image img)) return;
            if (UiEffects.GrayscaleMaterial) img.material = UiEffects.GrayscaleMaterial;
        }

        private static string BuildResultText(string header, string angle, string count, string direction, string status)
            => $"{header}\n\n각도: [{angle}]\n가동 수: [{count}]\n방향: [{direction}]\n\n전력 수급 상태: {status}";

        private async UniTaskVoid PlaySequence()
        {
            CancellationToken ct = destroyCancellationToken;
            try
            {
                await playerText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(playerImageGroup, 0f, 1f, fadeDuration, ct);
                await aiText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(aiImageGroup, 0f, 1f, fadeDuration, ct);

                if (videoPlayer) videoPlayer.Play();
                if (videoPanelGroup)
                    await SceneFader.FadeCanvasGroupAsync(videoPanelGroup, 0f, 1f, fadeDuration, ct);

                await SceneFader.FadeCanvasGroupAsync(confirmButtonGroup, 0f, 1f, fadeDuration, ct);
                SceneFader.SetGroupInteractable(confirmButtonGroup, true);

                await confirmButton.OnClickAsync(ct);
                // 크로스페이드 중 재클릭 방지
                SceneFader.SetGroupInteractable(confirmButtonGroup, false);
                await SceneFader.CrossFadeGroupsAsync(resultPanel, completePanel, fadeDuration, ct);
            }
            catch (OperationCanceledException)
            {
                // 시퀀스 도중 씬 전환(다음 버튼 등)으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }
    }
}
