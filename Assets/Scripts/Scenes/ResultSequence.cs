using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace DG.Scenes
{
    public class ResultSequence : MonoBehaviour
    {
        [SerializeField] private TypewriterTextTMP playerText;
        [SerializeField] private CanvasGroup playerImageGroup;
        [SerializeField] private TypewriterTextTMP aiText;
        [SerializeField] private CanvasGroup aiImageGroup;
        [SerializeField] private CanvasGroup aiResultGroup;
        [SerializeField] private CanvasGroup playerEffGroup;
        [SerializeField] private TextMeshProUGUI playerEffText;
        [SerializeField] private CanvasGroup aiEffGroup;
        [SerializeField] private TextMeshProUGUI aiEffText;
        [SerializeField] private SolarPanelPose playerPanelPose;
        [SerializeField] private SolarPanelPose aiPanelPose;
        [SerializeField] private float effCountDuration = 0.8f;
        [SerializeField] private float fadeDuration = 0.5f;

        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button nextButton;
        [SerializeField] private CanvasGroup confirmButtonGroup;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup aiStartPanel;
        [SerializeField] private TMP_Text aiStartText;
        [SerializeField] private float aiStartHold = 3f;

        [Inject] private GameSession _session;

        private int _playerPercent;
        private Material _grayscaleInstance;   // 흑백 전환용 머티리얼 인스턴스 (null이면 컬러 유지)

        private void Start()
        {
            // CompletePanel은 크로스페이드 전까지 투명 상태 — 미리 입력을 막아 ResultPanel을 가리지 않도록
            SceneFader.SetGroupInteractable(completePanel, false);

            // 확인 버튼은 시퀀스에서 페이드인 완료 후에만 입력 가능
            confirmButtonGroup.alpha = 0f;
            SceneFader.SetGroupInteractable(confirmButtonGroup, false);

            if (aiStartPanel)
            {
                aiStartPanel.alpha = 0f;
                SceneFader.SetGroupInteractable(aiStartPanel, false);
            }

            // AI 결과 패널은 AI 시작 안내 연출 후 페이드인 — 초기엔 숨김
            if (aiResultGroup)
                aiResultGroup.alpha = 0f;

            // 에너지 효율 텍스트는 각 이미지 페이드인 후 별도로 페이드인 — 초기엔 숨김
            if (playerEffGroup) playerEffGroup.alpha = 0f;
            if (aiEffGroup) aiEffGroup.alpha = 0f;

            // 태양광 패널은 기본 자세(평평·정면)에서 시작 — 값에 맞춰 이후 애니메이션
            if (playerPanelPose) playerPanelPose.SetNeutral();
            if (aiPanelPose) aiPanelPose.SetNeutral();

            if (nextButton)
                nextButton.onClick.AddListener(OnNextClicked);

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
                _playerPercent = Mathf.Clamp(Mathf.FloorToInt(percent), 0, 100);

                playerText.SetText(BuildResultText(
                    _session.lastAngle, _session.lastCount, _session.lastDirection, status));

                if (status == "부족")
                    ApplyGrayscale();
            }
            else
            {
                // 스킵/코딩 미완료 — 효율 0% 고정
                _playerPercent = 0;
                playerText.SetText("-\n\n전력 수급 상태: -");
                ApplyGrayscale();
            }

            aiText.SetText(BuildResultText(
                BlockScorer.GetBestAngle(),
                BlockScorer.GetBestCount(),
                BlockScorer.GetBestDirection(_session.lastQuestionTime),
                "양호"));
        }

        // 전력 부족 판정 — 머티리얼만 할당 (amount=0, 컬러 유지). 서서히 흑백 전환은 시퀀스에서.
        private void ApplyGrayscale()
        {
            if (!playerImageGroup.TryGetComponent<RawImage>(out RawImage img)) return;
            if (!UiEffects.GrayscaleMaterial) return;
            _grayscaleInstance = new Material(UiEffects.GrayscaleMaterial);
            _grayscaleInstance.SetFloat("_GrayscaleAmount", 0f);
            img.material = _grayscaleInstance;
        }

        // 애니메이션 완료 후 서서히 흑백으로 전환
        private async UniTask FadeToGrayscaleAsync(CancellationToken ct)
        {
            if (!_grayscaleInstance) return;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                _grayscaleInstance.SetFloat("_GrayscaleAmount", Mathf.Clamp01(t / fadeDuration));
                await UniTask.Yield(ct);
            }
            _grayscaleInstance.SetFloat("_GrayscaleAmount", 1f);
        }

        private static string BuildResultText(string angle, string count, string direction, string status)
            => $"각도: [{angle}]\n가동 수: [{count}]\n방향: [{direction}]\n\n전력 수급 상태: {status}";

        private async UniTaskVoid PlaySequence()
        {
            CancellationToken ct = destroyCancellationToken;
            try
            {
                await playerText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(playerImageGroup, 0f, 1f, fadeDuration, ct);
                if (playerPanelPose)
                    await playerPanelPose.ApplyAsync(_session ? _session.lastAngle : null, _session ? _session.lastDirection : null, ct);
                await PlayEfficiencyAsync(playerEffGroup, playerEffText, _playerPercent, ct);
                await FadeToGrayscaleAsync(ct);

                await PlayAiStartAsync(ct);

                // AI 시작 안내가 사라진 뒤 AI 결과 패널 페이드인 → 연출 시작
                if (aiResultGroup)
                    await SceneFader.FadeCanvasGroupAsync(aiResultGroup, 0f, 1f, fadeDuration, ct);

                await aiText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(aiImageGroup, 0f, 1f, fadeDuration, ct);
                if (aiPanelPose)
                    await aiPanelPose.ApplyAsync(BlockScorer.GetBestAngle(), BlockScorer.GetBestDirection(_session ? _session.lastQuestionTime : null), ct);
                await PlayEfficiencyAsync(aiEffGroup, aiEffText, 100, ct);

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

        // AI 코딩 시작 안내 패널 — 페이드인 → 점(0~3) 반복 애니메이션 → 약 aiStartHold초 후 페이드아웃
        private async UniTask PlayAiStartAsync(CancellationToken ct)
        {
            if (!aiStartPanel) return;

            using CancellationTokenSource dotCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            AnimateDotsAsync(dotCts.Token).Forget();

            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 0f, 1f, fadeDuration, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(aiStartHold), cancellationToken: ct);
            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 1f, 0f, fadeDuration, ct);

            dotCts.Cancel();
        }

        // 'AI가 코딩을 시작합니다' 뒤 점 개수를 0→3 반복 (취소될 때까지)
        private async UniTaskVoid AnimateDotsAsync(CancellationToken ct)
        {
            const string baseText = "AI가 코딩을 시작합니다";
            int n = 0;
            try
            {
                while (true)
                {
                    if (aiStartText) aiStartText.text = baseText + new string('.', n);
                    n = (n + 1) % 4;
                    await UniTask.Delay(400, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        // 에너지 효율 텍스트 — 페이드인 후 0%에서 target%까지 카운트업
        private async UniTask PlayEfficiencyAsync(CanvasGroup group, TextMeshProUGUI text, int target, CancellationToken ct)
        {
            if (!group || !text) return;

            text.text = "에너지 효율:00%";
            await SceneFader.FadeCanvasGroupAsync(group, 0f, 1f, fadeDuration, ct);

            float elapsed = 0f;
            while (elapsed < effCountDuration)
            {
                elapsed += Time.deltaTime;
                int p = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0f, target, elapsed / effCountDuration)), 0, target);
                text.text = $"에너지 효율:{p:D2}%";
                await UniTask.Yield(ct);
            }
            text.text = $"에너지 효율:{target:D2}%";
        }
    }
}
