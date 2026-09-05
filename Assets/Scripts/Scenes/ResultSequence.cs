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
using Wonjeong.Core;
using Wonjeong.Utils;

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
        // TODO: 레벨2(풍력) 결과 스테이지 — 3D 풍차 모델을 아직 4_Result에 배치하지 않았다.
        //       지금은 레벨과 무관하게 태양광 패널(ResultStage3D/PlayerPanel·AiPanel)만 회전한다.
        //       풍차를 배치할 때 레벨별로 스테이지를 교체하는 구조와, SolarPanelModelPose에 대응하는
        //       풍차용 방향(yaw) 제어 컴포넌트가 필요하다. (WindTurbineSpin은 날개 회전만 담당)
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
        [SerializeField] private TMP_Text topText;

        private GameSession _session;
        private InactivityTimer _inactivityTimer;

        [Inject]
        public void Construct(GameSession session, InactivityTimer inactivityTimer)
        {
            _session = session;
            _inactivityTimer = inactivityTimer;
        }

        private const int MaxPercent = 100;

        // 셰이더 프로퍼티 조회 비용을 줄이기 위한 ID 캐시
        private static readonly int GrayscaleAmountId = Shader.PropertyToID("_GrayscaleAmount");

        private int _playerPercent;
        private Material _grayscaleInstance;   // 흑백 전환용 머티리얼 인스턴스 (null이면 컬러 유지)

        // 00_Common.json의 panelFadeDuration 사용 — 로드 전까지의 폴백 기본값
        private float _fadeDuration = 0.5f;

        // 4_Result.json 연출 타이밍 — null이면 인스펙터·Constants 기본값으로 동작한다
        private ResultSceneSettings _sceneSettings;

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

            ApplyTopText();
            AnimateTopDotsAsync(destroyCancellationToken).Forget();

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

            string levelName = _session && _session.currentLevel ? _session.currentLevel.name : null;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시.
            // 레벨2(풍력)·레벨3(수력)은 가동 수 블록이 없어 각자의 값 하나로 판단한다.
            bool hasCoding =
                IsWindLevel(levelName)       ? _session.lastDirection is not null :
                IsHydroLevel(levelName)      ? _session.lastGateHeight is not null :
                IsPowerPlantLevel(levelName) ? _session.lastScore > 0 :
                                               _session.lastCount is not null && _session.lastDirection is not null;

            if (hasCoding)
            {
                // 최고 점수 대비 비율로 전력 수급 상태 판정
                int maxScore = BlockScorer.GetMaxScore(levelName);
                float percent = maxScore > 0 ? _session.lastScore * 100f / maxScore : 0f;
                string status = ToStatusText(percent);
                _playerPercent = Mathf.Clamp(Mathf.FloorToInt(percent), 0, MaxPercent);

                playerText.SetText(BuildPlayerResultText(levelName, status));

                // 전력이 부족한 결과는 흑백으로 전환해 시각적으로 구분
                if (status == Constants.ResultMessages.StatusPoor)
                    ApplyGrayscale();
            }
            else
            {
                // 스킵/코딩 미완료 — 효율 0% 고정
                _playerPercent = 0;
                playerText.SetText(
                    IsWindLevel(levelName)       ? Constants.ResultMessages.WindNoResultText :
                    IsHydroLevel(levelName)      ? Constants.ResultMessages.HydroNoResultText :
                    IsPowerPlantLevel(levelName) ? Constants.ResultMessages.PowerPlantNoResultText :
                                                   Constants.ResultMessages.NoResultText);
                ApplyGrayscale();
            }

            aiText.SetText(BuildAiResultText(levelName));
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

        private static bool IsWindLevel(string levelName)
            => !string.IsNullOrEmpty(levelName) && levelName.Contains("WindData");

        private static bool IsHydroLevel(string levelName)
            => !string.IsNullOrEmpty(levelName) && levelName.Contains("HydroData");

        // TODO: 레벨5(미래에너지) 결과 연출은 기획 미정 — 정해지면 여기에 전용 분기를 추가할 것.
        //       채점은 레벨4와 같은 경로(ScorePowerPlant, 만점 30점)를 쓰지만 함수 블록이 추가되는 레벨이라
        //       결과 텍스트 항목은 따로 정해야 한다. 그때까지 레벨5는 표시할 값이 없어 '-'로 나온다.
        //       (05_FutureEnergyData의 resultTopText도 비어 있어 씬 기본 문구가 그대로 쓰인다)
        private static bool IsPowerPlantLevel(string levelName)
            => !string.IsNullOrEmpty(levelName) && levelName.Contains("PowerPlantData");

        // 레벨2(풍력) 결과 — 문제로 나온 바람 방향 / 플레이어가 맞춘 풍차 방향 / 반복하기 사용 여부.
        // 반복하기 없이는 컴파일이 막히므로 정상 플레이에서 반복 감지는 항상 ON이다.
        private static string BuildWindResultText(string windDirection, string bladeDirection, bool repeatUsed, string status)
            => string.Format(Constants.ResultMessages.WindResultTextFormat,
                windDirection,
                bladeDirection,
                repeatUsed ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                status);

        // 레벨3(수력) 결과 — 문제로 나온 강물 높이 / 플레이어가 만약 블록에 연결한 수문 개방 높이.
        // 조건 감지는 조건 블록 연결 여부 — 조건 없이는 컴파일이 막히므로 정상 플레이에선 항상 ON이다.
        private static string BuildHydroResultText(string riverHeight, string gateHeight, string status)
            => string.Format(Constants.ResultMessages.HydroResultTextFormat,
                riverHeight,
                gateHeight,
                string.IsNullOrEmpty(gateHeight) ? Constants.ResultMessages.DetectedOff : Constants.ResultMessages.DetectedOn,
                status);

        // 레벨4(발전소) 결과 — 고정 상황 / 플레이어가 만약에 연결한 조건식 / 반복 중첩·병원 명령 위치.
        // 뒤 두 항목은 구조·명령 채점과 같은 기준이라 효율 %가 왜 그렇게 나왔는지 화면에서 읽힌다.
        private static string BuildPowerPlantResultText(string condition, bool repeatNested, bool hospitalInRepeat, string status)
            => string.Format(Constants.ResultMessages.PowerPlantResultTextFormat,
                Constants.ResultMessages.PowerPlantSituation,
                condition,
                repeatNested ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                hospitalInRepeat ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                status);

        private string BuildPlayerResultText(string levelName, string status)
        {
            if (IsWindLevel(levelName))
                return BuildWindResultText(_session.lastQuestionTime, _session.lastDirection, _session.lastRepeatUsed, status);

            if (IsHydroLevel(levelName))
                return BuildHydroResultText(_session.lastQuestionTime, _session.lastGateHeight, status);

            if (IsPowerPlantLevel(levelName))
                return BuildPowerPlantResultText(_session.lastConditionText, _session.lastRepeatNested,
                                                 _session.lastHospitalInRepeat, status);

            return BuildResultText(_session.lastCount, _session.lastDirection, status);
        }

        // AI는 항상 정답 — 풍력은 정답 방향, 수력은 문제와 같은 높이(정확히 일치가 최고점)
        private string BuildAiResultText(string levelName)
        {
            if (IsWindLevel(levelName))
                return BuildWindResultText(_session.lastQuestionTime,
                                           BlockScorer.GetBestDirection(_session.lastQuestionTime, levelName),
                                           true,
                                           Constants.ResultMessages.StatusGood);

            if (IsHydroLevel(levelName))
                return BuildHydroResultText(_session.lastQuestionTime,
                                            _session.lastQuestionTime,
                                            Constants.ResultMessages.StatusGood);

            if (IsPowerPlantLevel(levelName))
                return BuildPowerPlantResultText(Constants.ResultMessages.PowerPlantBestCondition,
                                                 true, true, Constants.ResultMessages.StatusGood);

            return BuildResultText(BlockScorer.GetBestCount(),
                                   BlockScorer.GetBestDirection(_session.lastQuestionTime, levelName),
                                   Constants.ResultMessages.StatusGood);
        }

        private async UniTaskVoid PlaySequence()
        {
            CancellationToken ct = destroyCancellationToken;
            try
            {
                // 확인 버튼이 열리기 전까지는 입력 없이 연출만 보는 구간 — 비활동 타이머를 멈춘다
                _inactivityTimer?.Pause();

                await LoadSceneSettingsAsync(ct);
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

                // 확인 버튼이 열렸으니 이제부터는 사용자 입력을 기다리는 구간 — 타이머 재개
                _inactivityTimer?.Resume();

                await confirmButton.OnClickAsync(ct);
                // 크로스페이드 중 재클릭 방지
                SceneFader.SetGroupInteractable(confirmButtonGroup, false);
                await SceneFader.CrossFadeGroupsAsync(resultPanel, completePanel, _fadeDuration, ct);
            }
            catch (OperationCanceledException)
            {
                // 시퀀스 도중 씬 전환(다음 버튼 등)으로 오브젝트가 파괴된 경우 — 정상 종료.
                // 확인 버튼이 열리기 전에 빠져나갔다면 타이머가 멈춘 채 남으므로 여기서 되돌린다.
                _inactivityTimer?.Resume();
            }
        }

        // AI 코딩 시작 안내 패널 — 페이드인 → 점(0~3) 반복 애니메이션 → 약 aiStartHold초 후 페이드아웃
        private async UniTask PlayAiStartAsync(CancellationToken ct)
        {
            if (!aiStartPanel) return;

            using CancellationTokenSource dotCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            AnimateDotsAsync(dotCts.Token).Forget();

            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 0f, 1f, _fadeDuration, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(_sceneSettings?.aiStartHold ?? aiStartHold), cancellationToken: ct);
            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 1f, 0f, _fadeDuration, ct);

            dotCts.Cancel();
        }

        // 'AI가 코딩을 시작합니다' 뒤 점 개수를 0→3 반복 (취소될 때까지).
        // 상단 문구와 같은 방식 — 문자열은 점 3개를 포함한 채로 두고 노출 개수만 바꿔 문구가 좌우로 흔들리지 않게 한다.
        private async UniTaskVoid AnimateDotsAsync(CancellationToken ct)
        {
            if (!aiStartText) return;

            aiStartText.text = Constants.ResultMessages.AiCodingStart + Constants.ResultMessages.AiCodingDots;
            aiStartText.ForceMeshUpdate();
            int baseLength = Mathf.Max(0, aiStartText.textInfo.characterCount - Constants.ResultMessages.AiCodingDots.Length);

            int dotCount = 0;
            try
            {
                while (true)
                {
                    aiStartText.maxVisibleCharacters = baseLength + dotCount;
                    dotCount = (dotCount + 1) % Constants.ResultMessages.AiCodingDotCycle;
                    await UniTask.Delay(_sceneSettings?.aiCodingDotIntervalMs ?? Constants.ResultMessages.AiCodingDotIntervalMs, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        // 4_Result.json 로드 — 타이프라이터·패널 자세처럼 다른 컴포넌트가 가진 값도 여기서 밀어넣는다
        private async UniTask LoadSceneSettingsAsync(CancellationToken ct)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Result}";
            _sceneSettings = await JsonLoader.LoadAsync<ResultSceneSettings>(path, ct);
            if (_sceneSettings is null) return;

            if (playerText) playerText.CharInterval = _sceneSettings.typewriterCharInterval;
            if (aiText) aiText.CharInterval = _sceneSettings.typewriterCharInterval;
            if (playerPanelPose) playerPanelPose.AnimDuration = _sceneSettings.panelPoseDuration;
            if (aiPanelPose) aiPanelPose.AnimDuration = _sceneSettings.panelPoseDuration;
        }

        // 상단 안내 문구 — 레벨별 문구 뒤에 말줄임 슬롯을 붙여둔다.
        // 문구가 비어 있는 레벨(미정)은 씬에 입력해둔 텍스트를 그대로 쓴다.
        private void ApplyTopText()
        {
            if (!topText) return;

            string baseText = _session && _session.currentLevel ? _session.currentLevel.resultTopText : null;
            if (string.IsNullOrEmpty(baseText))
                baseText = topText.text;

            // 씬 텍스트에 이미 말줄임이 들어 있으면 중복되지 않도록 떼어낸다
            if (baseText.EndsWith(Constants.ResultMessages.ResultTopDots))
                baseText = baseText[..^Constants.ResultMessages.ResultTopDots.Length];

            topText.text = baseText + Constants.ResultMessages.ResultTopDots;
            topText.ForceMeshUpdate();
        }

        // 상단 문구 뒤 점 개수를 0→3 반복 (씬이 끝날 때까지).
        // 문자열은 말줄임을 포함한 채로 두고 노출 개수만 바꾸므로 문구 폭이 고정되어 좌우로 흔들리지 않는다.
        private async UniTaskVoid AnimateTopDotsAsync(CancellationToken ct)
        {
            if (!topText) return;

            int baseLength = Mathf.Max(0, topText.textInfo.characterCount - Constants.ResultMessages.ResultTopDots.Length);
            int dotCount = 0;
            try
            {
                while (true)
                {
                    topText.maxVisibleCharacters =
                        baseLength + dotCount * Constants.ResultMessages.ResultTopDotSlotLength;
                    dotCount = (dotCount + 1) % (Constants.ResultMessages.ResultTopDotMax + 1);
                    await UniTask.Delay(_sceneSettings?.topDotIntervalMs ?? Constants.ResultMessages.ResultTopDotIntervalMs, cancellationToken: ct);
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
            await DOVirtual.Float(0f, target, _sceneSettings?.effCountDuration ?? effCountDuration, v =>
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
