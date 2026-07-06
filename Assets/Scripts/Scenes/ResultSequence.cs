using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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

        [Inject] private GameSession _session;

        private const int ResultHoldMillis = 1000;

        private void Start()
        {
            // CompletePanel은 크로스페이드 전까지 투명 상태 — 미리 입력을 막아 ResultPanel을 가리지 않도록
            SetPanelInteractable(completePanel, false);

            if (nextButton)
                nextButton.onClick.AddListener(OnNextClicked);

            ApplySessionResults();
            PlaySequence().Forget();
        }

        // 다음 레벨로 진행 — 방금 플레이한 레벨의 afterResultScene을 따라감 (마지막 레벨은 6_End)
        private void OnNextClicked()
        {
            string nextScene = _session && _session.currentLevel ? _session.currentLevel.afterResultScene : "3_Story";
            if (_session) _session.unlockedLevelIndex++;
            SceneManager.LoadScene(nextScene);
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

            Shader shader = Shader.Find("Custom/UI/Grayscale");
            if (shader) img.material = new Material(shader);
        }

        private static string BuildResultText(string header, string angle, string count, string direction, string status)
            => $"{header}\n\n각도: [{angle}]\n가동 수: [{count}]\n방향: [{direction}]\n\n전력 수급 상태: {status}";

        private async UniTaskVoid PlaySequence()
        {
            CancellationToken ct = destroyCancellationToken;
            try
            {
                await playerText.PlayAsync(ct);
                await FadeIn(playerImageGroup, ct);
                await aiText.PlayAsync(ct);
                await FadeIn(aiImageGroup, ct);

                await UniTask.Delay(ResultHoldMillis, cancellationToken: ct);
                await CrossFade(resultPanel, completePanel, fadeDuration, ct);
            }
            catch (OperationCanceledException)
            {
                // 시퀀스 도중 씬 전환(다음 버튼 등)으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        private async UniTask FadeIn(CanvasGroup group, CancellationToken ct)
        {
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Clamp01(t / fadeDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            group.alpha = 1;
        }

        // ResultPanel → CompletePanel 크로스페이드
        private async UniTask CrossFade(CanvasGroup from, CanvasGroup to, float duration, CancellationToken ct)
        {
            SetPanelInteractable(to, true);

            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                if (from) from.alpha = 1f - p;
                if (to) to.alpha = p;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (from) from.alpha = 0f;
            if (to) to.alpha = 1f;
            SetPanelInteractable(from, false);
        }

        private static void SetPanelInteractable(CanvasGroup group, bool value)
        {
            if (!group) return;
            group.interactable = value;
            group.blocksRaycasts = value;
        }
    }
}
