using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using Game.Runtime;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Scenes
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
        [SerializeField] private SolarPanelModelPose playerPanelPose;
        [SerializeField] private SolarPanelModelPose aiPanelPose;
        [SerializeField] private float effCountDuration = 0.8f;

        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button nextButton;
        [SerializeField] private CanvasGroup confirmButtonGroup;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup aiStartPanel;
        [SerializeField] private TMP_Text aiStartText;
        [SerializeField] private float aiStartHold = 3f;

        private GameSession _session;

        [Inject]
        public void Construct(GameSession session)
        {
            _session = session;
        }

        private const int MaxPercent = 100;

        // 셰이더 프로퍼티 조회 비용을 줄이기 위한 ID 캐시
        private static readonly int GrayscaleAmountId = Shader.PropertyToID("_GrayscaleAmount");

        private int _playerPercent;
        private Material _grayscaleInstance;   // 흑백 전환용 머티리얼 인스턴스 (null이면 컬러 유지)

        // 00_Common.json의 panelFadeDuration 사용 — 로드 전까지의 폴백 기본값
        private float _fadeDuration = 0.5f;

        private void Start()
        {
            // UI 작업 중 에디터에서 패널을 꺼둔 채 플레이해도 항상 resultPanel만 보이는 상태로 시작하도록 정규화.
            // CompletePanel은 크로스페이드 전까지 투명 상태 — 미리 입력을 막아 ResultPanel을 가리지 않도록
            SceneFader.InitializePanelState(resultPanel, true);
            SceneFader.InitializePanelState(completePanel, false);

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
            string nextScene = _session && _session.currentLevel ? _session.currentLevel.afterResultScene : Constants.Scenes.Story;
            if (_session) _session.unlockedLevelIndex++;
            SceneFader.FadeAndLoad(nextScene).Forget();
        }

        // 4_Game에서 저장한 결과로 텍스트 구성 — 거치지 않고 진입하면 씬 기본 텍스트 유지
        private void ApplySessionResults()
        {
            if (!_session || _session.lastQuestionTime is null)
                return;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시
            bool hasCoding = _session.lastCount is not null
                             && _session.lastDirection is not null;

            string levelName = _session && _session.currentLevel ? _session.currentLevel.name : null;

            if (hasCoding)
            {
                // 최고 점수 대비 비율로 전력 수급 상태 판정
                int maxScore = BlockScorer.GetMaxScore(levelName);
                float percent = maxScore > 0 ? _session.lastScore * 100f / maxScore : 0f;
                string status = ToStatusText(percent);
                _playerPercent = Mathf.Clamp(Mathf.FloorToInt(percent), 0, MaxPercent);

                playerText.SetText(BuildResultText(
                    _session.lastCount, _session.lastDirection, status));

                // 전력이 부족한 결과는 흑백으로 전환해 시각적으로 구분
                if (status == Constants.ResultMessages.StatusPoor)
                    ApplyGrayscale();
            }
            else
            {
                // 스킵/코딩 미완료 — 효율 0% 고정
                _playerPercent = 0;
                playerText.SetText(Constants.ResultMessages.NoResultText);
                ApplyGrayscale();
            }

            aiText.SetText(BuildResultText(
                BlockScorer.GetBestCount(),
                BlockScorer.GetBestDirection(_session.lastQuestionTime, levelName),
                Constants.ResultMessages.StatusGood));
        }

        // 전력 부족 판정 — 머티리얼만 할당 (amount=0, 컬러 유지). 서서히 흑백 전환은 시퀀스에서.
        private void ApplyGrayscale()
        {
            if (!playerImageGroup.TryGetComponent<RawImage>(out RawImage img)) return;
            if (!UiEffects.GrayscaleMaterial) return;
            _grayscaleInstance = new Material(UiEffects.GrayscaleMaterial);
            _grayscaleInstance.SetFloat(GrayscaleAmountId, 0f);
            img.material = _grayscaleInstance;
        }

        // 애니메이션 완료 후 서서히 흑백으로 전환
        private async UniTask FadeToGrayscaleAsync(CancellationToken ct)
        {
            if (!_grayscaleInstance) return;
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                _grayscaleInstance.SetFloat(GrayscaleAmountId, Mathf.Clamp01(t / _fadeDuration));
                await UniTask.Yield(ct);
            }
            _grayscaleInstance.SetFloat(GrayscaleAmountId, 1f);
        }

        // 최고 점수 대비 비율(%)을 전력 수급 상태 문구로 변환
        private static string ToStatusText(float percent) =>
            percent < Constants.ResultMessages.NormalThresholdPercent ? Constants.ResultMessages.StatusPoor :
            percent < Constants.ResultMessages.GoodThresholdPercent   ? Constants.ResultMessages.StatusNormal :
                                                                       Constants.ResultMessages.StatusGood;

        private static string BuildResultText(string count, string direction, string status)
            => string.Format(Constants.ResultMessages.ResultTextFormat, count, direction, status);

        private async UniTaskVoid PlaySequence()
        {
            CancellationToken ct = destroyCancellationToken;
            try
            {
                _fadeDuration = await SceneFader.GetPanelFadeDurationAsync();

                await playerText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(playerImageGroup, 0f, 1f, _fadeDuration, ct);
                if (playerPanelPose)
                    await playerPanelPose.ApplyAsync(null, _session ? _session.lastDirection : null, ct);
                await PlayEfficiencyAsync(playerEffGroup, playerEffText, _playerPercent, ct);
                await FadeToGrayscaleAsync(ct);

                await PlayAiStartAsync(ct);

                // AI 시작 안내가 사라진 뒤 AI 결과 패널 페이드인 → 연출 시작
                if (aiResultGroup)
                    await SceneFader.FadeCanvasGroupAsync(aiResultGroup, 0f, 1f, _fadeDuration, ct);

                await aiText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(aiImageGroup, 0f, 1f, _fadeDuration, ct);
                string levelName = _session && _session.currentLevel ? _session.currentLevel.name : null;
                if (aiPanelPose)
                    await aiPanelPose.ApplyAsync(null, BlockScorer.GetBestDirection(_session ? _session.lastQuestionTime : null, levelName), ct);
                await PlayEfficiencyAsync(aiEffGroup, aiEffText, MaxPercent, ct);

                await SceneFader.FadeCanvasGroupAsync(confirmButtonGroup, 0f, 1f, _fadeDuration, ct);
                SceneFader.SetGroupInteractable(confirmButtonGroup, true);

                await confirmButton.OnClickAsync(ct);
                // 크로스페이드 중 재클릭 방지
                SceneFader.SetGroupInteractable(confirmButtonGroup, false);
                await SceneFader.CrossFadeGroupsAsync(resultPanel, completePanel, _fadeDuration, ct);
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

            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 0f, 1f, _fadeDuration, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(aiStartHold), cancellationToken: ct);
            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 1f, 0f, _fadeDuration, ct);

            dotCts.Cancel();
        }

        // 'AI가 코딩을 시작합니다' 뒤 점 개수를 0→3 반복 (취소될 때까지)
        private async UniTaskVoid AnimateDotsAsync(CancellationToken ct)
        {
            string baseText = Constants.ResultMessages.AiCodingStart;
            int dotCount = 0;
            try
            {
                while (true)
                {
                    if (aiStartText) aiStartText.text = baseText + new string('.', dotCount);
                    dotCount = (dotCount + 1) % Constants.ResultMessages.AiCodingDotCycle;
                    await UniTask.Delay(Constants.ResultMessages.AiCodingDotIntervalMs, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        // 에너지 효율 텍스트 — 페이드인 후 0%에서 target%까지 카운트업
        private async UniTask PlayEfficiencyAsync(CanvasGroup group, TextMeshProUGUI text, int target, CancellationToken ct)
        {
            if (!group || !text) return;

            text.text = FormatEfficiency(0);
            await SceneFader.FadeCanvasGroupAsync(group, 0f, 1f, _fadeDuration, ct);

            // 빠르게 오르다 끝에서 감속하는 카운터 연출 (순수 수치 보간이므로 DOVirtual)
            await DOVirtual.Float(0f, target, effCountDuration, v =>
                {
                    text.text = FormatEfficiency(Mathf.Clamp(Mathf.RoundToInt(v), 0, target));
                })
                .SetEase(Ease.OutQuad)
                .SetLink(text.gameObject)
                .ToUniTask(cancellationToken: ct);
            text.text = FormatEfficiency(target);
        }

        private static string FormatEfficiency(int percent)
            => string.Format(Constants.ResultMessages.EfficiencyFormat, percent);
    }
}
