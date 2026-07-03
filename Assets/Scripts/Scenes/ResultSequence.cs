using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class ResultSequence : MonoBehaviour
    {
        [SerializeField] private TypewriterText playerText;
        [SerializeField] private CanvasGroup playerImageGroup;
        [SerializeField] private TypewriterText aiText;
        [SerializeField] private CanvasGroup aiImageGroup;
        [SerializeField] private float fadeDuration = 0.5f;

        private void Start()
        {
            ApplySessionResults();
            PlaySequence().Forget();
        }

        // 4_Game에서 저장한 결과로 텍스트 구성 — 거치지 않고 진입하면 씬 기본 텍스트 유지
        private void ApplySessionResults()
        {
            GameSession session = GameSession.Instance;
            if (!session || session.lastQuestionTime is null)
                return;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시
            bool hasCoding = session.lastAngle is not null
                             && session.lastCount is not null
                             && session.lastDirection is not null;

            if (hasCoding)
            {
                // 최고 점수 대비 비율로 전력 수급 상태 판정
                int maxScore = BlockScorer.GetMaxScore();
                float percent = maxScore > 0 ? session.lastScore * 100f / maxScore : 0f;
                string status = percent < 50f ? "부족" : percent < 80f ? "보통" : "양호";

                playerText.SetText(BuildResultText("[체험자 코딩]",
                    session.lastAngle, session.lastCount, session.lastDirection, status));

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
                BlockScorer.GetBestDirection(session.lastQuestionTime),
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
            await playerText.PlayAsync();
            await FadeIn(playerImageGroup);
            await aiText.PlayAsync();
            await FadeIn(aiImageGroup);
        }

        private async UniTask FadeIn(CanvasGroup group)
        {
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Clamp01(t / fadeDuration);
                await UniTask.Yield();
            }
            group.alpha = 1;
        }
    }
}
