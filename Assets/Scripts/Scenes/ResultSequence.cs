using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using Game.Runtime;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Core;
using HuliacDev.Utils;
using ZLogger;

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
        // 레벨별 3D 결과 스테이지 — 모델과 전용 카메라(RenderTexture 공유)를 통째로 켜고 끈다.
        // 모든 스테이지의 카메라가 같은 RT를 노리므로 반드시 한 스테이지만 활성 상태여야 한다.
        // 전용 모델이 없는 레벨(1·5)은 태양광 스테이지를 그대로 쓴다.
        [SerializeField] private GameObject solarStage;
        [SerializeField] private GameObject windStage;
        [SerializeField] private GameObject hydroStage;
        [SerializeField] private GameObject plantStage;
        [SerializeField] private SolarPanelModelPose playerPanelPose;
        [SerializeField] private SolarPanelModelPose aiPanelPose;
        [SerializeField] private WindTurbineSpin playerTurbineSpin;
        [SerializeField] private WindTurbineSpin aiTurbineSpin;
        [SerializeField] private DamGateFlow playerDamGates;
        [SerializeField] private DamGateFlow aiDamGates;
        [SerializeField] private PowerPlantPump playerPlantPump;
        [SerializeField] private PowerPlantPump aiPlantPump;
        [SerializeField] private float effCountDuration = 0.8f;

        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private CanvasGroup completePanel;
        [Tooltip("완료 패널 제목 텍스트 — 결과에 따라 '미션 성공!' / '미션 실패!'로 바뀐다")]
        [SerializeField] private TMP_Text completeTitleText;
        [SerializeField] private Button nextButton;
        [SerializeField] private CanvasGroup confirmButtonGroup;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup aiStartPanel;
        [SerializeField] private TMP_Text aiStartText;
        [SerializeField] private float aiStartHold = 3f;
        [SerializeField] private TMP_Text topText;

        private GameSession _session;
        private InactivityTimer _inactivityTimer;
        private ILogger<ResultSequence> _logger;

        /// <summary>
        /// 게임 세션, 비활동 타이머, 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(GameSession session, InactivityTimer inactivityTimer, ILogger<ResultSequence> logger)
        {
            _session = session;
            _inactivityTimer = inactivityTimer;
            _logger = logger;
        }

        private const int MaxPercent = 100;

        private string CurrentLevelName => _session && _session.currentLevel ? _session.currentLevel.name : null;

        // 셰이더 프로퍼티 조회 비용을 줄이기 위한 ID 캐시
        private static readonly int GrayscaleAmountId = Shader.PropertyToID("_GrayscaleAmount");

        private int _playerPercent;
        private bool _isWindStage;             // 풍력 스테이지로 연출 중인지 (레벨2)
        private bool _isHydroStage;            // 수력 스테이지로 연출 중인지 (레벨3)
        private bool _isPlantStage;            // 발전소 스테이지로 연출 중인지 (레벨4)
        private Material _grayscaleInstance;   // 흑백 전환용 머티리얼 인스턴스 (null이면 컬러 유지)

        // 00_Common.json의 panelFadeDuration 사용 — 로드 전까지의 폴백 기본값
        private float _fadeDuration = 0.5f;

        // 4_Result.json 연출 타이밍 — null이면 인스펙터·Constants 기본값으로 동작한다
        private ResultSceneSettings _sceneSettings;

        /// <summary>
        /// 표시 상태를 초기화하고 결과 텍스트를 구성한 뒤 결과 연출 시퀀스를 시작한다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[ResultSequence] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            WarnMissingReferences();
            InitializeSceneState();

            if (nextButton)
                nextButton.onClick.AddListener(OnNextClicked);

            ApplyTopText();
            AnimateTopDotsAsync(destroyCancellationToken).Forget();

            ApplySessionResults();
            PlaySequence().Forget();
        }

        /// <summary>
        /// 시퀀스 진행에 꼭 필요한 인스펙터 참조가 빠져 있으면 경고를 남긴다.
        /// </summary>
        private void WarnMissingReferences()
        {
            if (_logger == null) return;

            if (!playerText) _logger.ZLogWarning($"[ResultSequence] playerText가 할당되지 않았습니다.");
            if (!aiText) _logger.ZLogWarning($"[ResultSequence] aiText가 할당되지 않았습니다.");
            if (!playerImageGroup) _logger.ZLogWarning($"[ResultSequence] playerImageGroup이 할당되지 않았습니다.");
            if (!confirmButton) _logger.ZLogWarning($"[ResultSequence] confirmButton이 할당되지 않았습니다.");
            if (!nextButton) _logger.ZLogWarning($"[ResultSequence] nextButton이 할당되지 않아 다음 씬으로 넘어갈 수 없습니다.");
        }

        /// <summary>
        /// 시퀀스가 건드리는 모든 표시 상태를 첫 입장 기준으로 되돌린다.
        /// 에디터에서 연출 중간 값(alpha, 꺼둔 패널, 흑백 머티리얼 등)을 저장해두고 플레이해도
        /// 항상 같은 그림에서 연출이 시작되도록, 씬에 저장된 값에 의존하지 않는 것이 목적이다.
        /// </summary>
        private void InitializeSceneState()
        {
            // 레벨에 맞는 3D 스테이지를 먼저 켠다 — 아래 SetNeutral()이 활성화된 컴포넌트에 닿도록
            ApplyLevelStage();

            // 결과 패널만 보이는 상태로 시작. CompletePanel은 크로스페이드 전까지 투명 —
            // 미리 입력을 막아 ResultPanel을 가리지 않도록
            SceneFader.InitializePanelState(resultPanel, true);
            SceneFader.InitializePanelState(completePanel, false);

            // 시퀀스가 순서대로 페이드인하는 것들 — 전부 투명·입력 차단 상태에서 시작.
            // (플레이어 이미지 → 효율 → AI 시작 안내 → AI 결과/이미지/효율 → 확인 버튼)
            SceneFader.InitializePanelState(playerImageGroup, false);
            SceneFader.InitializePanelState(playerEffGroup, false);
            SceneFader.InitializePanelState(aiStartPanel, false);
            SceneFader.InitializePanelState(aiResultGroup, false);
            SceneFader.InitializePanelState(aiImageGroup, false);
            SceneFader.InitializePanelState(aiEffGroup, false);
            SceneFader.InitializePanelState(confirmButtonGroup, false);

            // 효율 텍스트는 0%에서 카운트업 — 씬에 남은 값이 페이드인 첫 프레임에 비치지 않도록
            if (playerEffText) playerEffText.text = FormatEfficiency(0);
            if (aiEffText) aiEffText.text = FormatEfficiency(0);

            // 흑백 머티리얼은 판정 결과에 따라 런타임에만 붙인다 — 씬에 남아 있으면 컬러로 되돌린다
            if (playerImageGroup && playerImageGroup.TryGetComponent(out RawImage playerImage))
                playerImage.material = null;

            // 태양광 패널은 기본 자세(평평·정면), 풍차는 정지, 댐 수문은 닫힌 상태에서 시작
            if (playerPanelPose) playerPanelPose.SetNeutral();
            if (aiPanelPose) aiPanelPose.SetNeutral();
            if (playerTurbineSpin) playerTurbineSpin.SetNeutral();
            if (aiTurbineSpin) aiTurbineSpin.SetNeutral();
            if (playerDamGates) playerDamGates.SetNeutral();
            if (aiDamGates) aiDamGates.SetNeutral();
            if (playerPlantPump) playerPlantPump.SetNeutral();
            if (aiPlantPump) aiPlantPump.SetNeutral();
        }

        /// <summary>
        /// 흑백 머티리얼 인스턴스를 정리하고, 멈춰 있을 수 있는 비활동 타이머를 재개한다.
        /// </summary>
        private void OnDestroy()
        {
            // 흑백 전환용 머티리얼은 런타임에 new로 만든 인스턴스라 씬 언로드로 회수되지 않는다
            if (_grayscaleInstance)
            {
                Destroy(_grayscaleInstance);
                _grayscaleInstance = null;
            }

            if (nextButton)
                nextButton.onClick.RemoveListener(OnNextClicked);

            // 확인 버튼이 열리기 전에 파괴됐다면 타이머가 멈춘 채 남는다 —
            // 이미 재개된 상태에서 다시 불러도 카운트만 처음부터 다시 시작할 뿐 부작용이 없다
            if (_inactivityTimer) _inactivityTimer.Resume();
        }

        /// <summary>
        /// 다음 레벨로 진행한다 — 방금 플레이한 레벨의 AfterResultScene을 따라간다 (마지막 레벨은 5_Outro).
        /// </summary>
        private void OnNextClicked()
        {
            string nextScene = _session && _session.currentLevel ? _session.currentLevel.AfterResultScene : Constants.Scenes.Story;

            // 이미 해금된 이전 레벨을 다시 플레이한 경우엔 진행도를 건드리지 않는다.
            // 무조건 +1 하면 재플레이만으로 아직 깨지 않은 레벨까지 해금돼버린다.
            if (_session && _session.currentLevel)
                _session.unlockedLevelIndex = Mathf.Max(_session.unlockedLevelIndex, _session.currentLevel.levelIndex + 1);
            else if (_logger != null)
                _logger.ZLogWarning($"[ResultSequence] 현재 레벨 정보가 없어 진행도를 갱신하지 않고 {nextScene}(으)로 이동합니다.");
            SceneFader.FadeAndLoad(nextScene, logger: _logger).Forget();
        }

        /// <summary>
        /// 레벨에 맞는 3D 스테이지만 남긴다 — 풍력(레벨2)은 풍차, 수력(레벨3)은 댐,
        /// 발전소(레벨4)는 발전소 건물, 나머지는 태양광 패널.
        /// </summary>
        private void ApplyLevelStage()
        {
            string levelName = CurrentLevelName;
            _isWindStage = Constants.Levels.IsWind(levelName);
            _isHydroStage = Constants.Levels.IsHydro(levelName);
            // TODO: 레벨5(미래에너지) 결과 연출은 기획 미정 — 정해지면 여기에 전용 분기를 추가할 것.
            //       채점은 레벨4와 같은 경로(ScorePowerPlant, 만점 30점)를 쓰지만 함수 블록이 추가되는 레벨이라
            //       결과 텍스트 항목은 따로 정해야 한다. 그때까지 레벨5는 표시할 값이 없어 '-'로 나온다.
            //       (05_FutureEnergyData의 resultTopText도 비어 있어 씬 기본 문구가 그대로 쓰인다)
            _isPlantStage = Constants.Levels.IsPowerPlant(levelName);

            if (solarStage) solarStage.SetActive(!_isWindStage && !_isHydroStage && !_isPlantStage);
            if (windStage) windStage.SetActive(_isWindStage);
            if (hydroStage) hydroStage.SetActive(_isHydroStage);
            if (plantStage) plantStage.SetActive(_isPlantStage);
        }

        /// <summary>
        /// 게임 씬에서 저장한 결과로 플레이어·AI 결과 텍스트를 구성한다 (거치지 않고 진입하면 씬 기본 텍스트 유지).
        /// </summary>
        private void ApplySessionResults()
        {
            if (!_session || _session.lastQuestionTime is null)
            {
                if (_logger != null) _logger.ZLogInformation($"[ResultSequence] 게임 씬 결과가 없어 씬 기본 텍스트를 그대로 표시합니다.");
                return;
            }

            string levelName = CurrentLevelName;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시.
            // 레벨마다 채워지는 값이 달라 개별 필드로 판정하지 않고 게임 씬이 세운 플래그를 그대로 쓴다.
            bool hasCoding = _session.hasCodingResult;
            string playerResult;
            bool isSuccess = false;

            if (hasCoding)
            {
                // 최고 점수 대비 비율로 전력 수급 상태 판정
                int maxScore = BlockScorer.GetMaxScore(levelName);
                float percent = maxScore > 0 ? _session.lastScore * 100f / maxScore : 0f;
                string status = ToStatusText(percent);
                _playerPercent = Mathf.Clamp(Mathf.FloorToInt(percent), 0, MaxPercent);

                playerResult = BuildPlayerResultText(levelName, status);
                isSuccess = status != Constants.ResultMessages.StatusPoor;

                // 전력이 부족한 결과는 흑백으로 전환해 시각적으로 구분
                if (status == Constants.ResultMessages.StatusPoor)
                    ApplyGrayscale();
            }
            else
            {
                // 스킵/코딩 미완료 — 효율 0% 고정
                _playerPercent = 0;
                playerResult =
                    Constants.Levels.IsWind(levelName)       ? BuildWindNoResultText() :
                    Constants.Levels.IsHydro(levelName)      ? BuildHydroNoResultText() :
                    Constants.Levels.IsPowerPlant(levelName) ? Constants.ResultMessages.PowerPlantNoResultText :
                                                               Constants.ResultMessages.NoResultText;
                ApplyGrayscale();
            }

            if (playerText) playerText.SetText(playerResult);
            if (aiText) aiText.SetText(BuildAiResultText(levelName));

            if (completeTitleText)
                completeTitleText.text = isSuccess ? Constants.ResultMessages.MissionSuccess : Constants.ResultMessages.MissionFail;
            else if (_logger != null)
                _logger.ZLogWarning($"[ResultSequence] completeTitleText가 할당되지 않아 미션 성공/실패를 표시하지 못했습니다.");
        }

        /// <summary>
        /// 전력 부족 판정 시 흑백 머티리얼만 할당한다 (amount=0, 컬러 유지 — 서서히 흑백 전환은 시퀀스에서).
        /// </summary>
        private void ApplyGrayscale()
        {
            if (!playerImageGroup || !playerImageGroup.TryGetComponent<RawImage>(out RawImage img))
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] playerImageGroup에 RawImage가 없어 흑백 전환을 건너뜁니다.");
                return;
            }
            if (!UiEffects.GrayscaleMaterial)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] 흑백 머티리얼을 만들 수 없어 흑백 전환을 건너뜁니다.");
                return;
            }
            _grayscaleInstance = new Material(UiEffects.GrayscaleMaterial);
            _grayscaleInstance.SetFloat(GrayscaleAmountId, 0f);
            img.material = _grayscaleInstance;
        }

        /// <summary>
        /// 애니메이션 완료 후 서서히 흑백으로 전환한다 (흑백 판정이 아니면 아무것도 하지 않음).
        /// </summary>
        private async UniTask FadeToGrayscaleAsync(CancellationToken ct)
        {
            if (!_grayscaleInstance) return;

            // 머티리얼 수치 보간이라 트윈 대상이 없으므로 DOVirtual로 처리한다
            await DOVirtual.Float(0f, 1f, _fadeDuration, v => _grayscaleInstance.SetFloat(GrayscaleAmountId, v))
                .SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            _grayscaleInstance.SetFloat(GrayscaleAmountId, 1f);
        }

        /// <summary>
        /// 최고 점수 대비 비율(%)을 전력 수급 상태 문구로 변환한다.
        /// </summary>
        private static string ToStatusText(float percent) =>
            percent < Constants.ResultMessages.NormalThresholdPercent ? Constants.ResultMessages.StatusPoor :
            percent < Constants.ResultMessages.GoodThresholdPercent   ? Constants.ResultMessages.StatusNormal :
                                                                       Constants.ResultMessages.StatusGood;

        /// <summary>
        /// 레벨1(태양광) 결과 문구를 만든다 — 설치 개수 / 패널 방향 / 전력 수급 상태.
        /// </summary>
        private static string BuildResultText(string count, string direction, string status)
            => ZString.Format(Constants.ResultMessages.ResultTextFormat, count, direction, status);

        /// <summary>
        /// 레벨2(풍력) 결과 문구를 만든다 — 문제로 나온 바람 방향 / 플레이어가 맞춘 풍차 방향 / 반복하기 사용 여부.
        /// 반복하기 없이는 컴파일이 막히므로 정상 플레이에서 반복 감지는 항상 ON이다.
        /// </summary>
        private static string BuildWindResultText(string windDirection, string bladeDirection, bool repeatUsed, string status)
            => ZString.Format(Constants.ResultMessages.WindResultTextFormat,
                windDirection,
                bladeDirection,
                repeatUsed ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                status);

        /// <summary>
        /// 레벨3(수력) 결과 문구를 만든다 — 문제로 나온 강물 높이 / 플레이어가 만약 블록에 연결한 수문 개방 높이 / 아니면 사용 여부.
        /// 조건 감지는 조건 블록 연결 여부 — 조건 없이는 컴파일이 막히므로 정상 플레이에선 항상 ON이다.
        /// 아니면은 채점 항목이라 빠뜨렸을 때 AI 결과와 달라 보이도록 따로 표시한다.
        /// </summary>
        private static string BuildHydroResultText(string riverHeight, string gateHeight, bool elseUsed, string status)
            => ZString.Format(Constants.ResultMessages.HydroResultTextFormat,
                riverHeight,
                gateHeight,
                string.IsNullOrEmpty(gateHeight) ? Constants.ResultMessages.DetectedOff : Constants.ResultMessages.DetectedOn,
                elseUsed ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                status);

        /// <summary>
        /// 레벨4(발전소) 결과 문구를 만든다 — 고정 상황 / 플레이어가 만약에 연결한 조건식 / 반복 중첩·병원 명령 위치.
        /// 뒤 두 항목은 구조·명령 채점과 같은 기준이라 효율 %가 왜 그렇게 나왔는지 화면에서 읽힌다.
        /// </summary>
        private static string BuildPowerPlantResultText(string condition, bool repeatNested, bool hospitalInRepeat, string status)
            => ZString.Format(Constants.ResultMessages.PowerPlantResultTextFormat,
                Constants.ResultMessages.PowerPlantSituation,
                condition,
                repeatNested ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                hospitalInRepeat ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff,
                status);

        /// <summary>
        /// 레벨2(풍력) 스킵 문구에 문제로 주어진 바람 방향을 채워 넣는다.
        /// 이 분기는 lastQuestionTime이 있을 때만 도달하므로 값이 비어 있을 일은 없다.
        /// </summary>
        private string BuildWindNoResultText()
            => ZString.Format(Constants.ResultMessages.WindNoResultTextFormat, _session.lastQuestionTime);

        /// <summary>
        /// 레벨3(수력) 스킵 문구에 문제로 주어진 강물 높이를 채워 넣는다.
        /// </summary>
        private string BuildHydroNoResultText()
            => ZString.Format(Constants.ResultMessages.HydroNoResultTextFormat, _session.lastQuestionTime);

        /// <summary>
        /// 레벨에 맞는 플레이어 결과 문구를 세션 값으로 만든다.
        /// </summary>
        private string BuildPlayerResultText(string levelName, string status)
        {
            if (Constants.Levels.IsWind(levelName))
                return BuildWindResultText(_session.lastQuestionTime, _session.lastDirection, _session.lastRepeatUsed, status);

            if (Constants.Levels.IsHydro(levelName))
                return BuildHydroResultText(_session.lastQuestionTime, _session.lastGateHeight, _session.lastElseUsed, status);

            if (Constants.Levels.IsPowerPlant(levelName))
                return BuildPowerPlantResultText(_session.lastConditionText, _session.lastRepeatNested,
                                                 _session.lastHospitalInRepeat, status);

            return BuildResultText(_session.lastCount, _session.lastDirection, status);
        }

        /// <summary>
        /// AI 결과 문구를 만든다 — AI는 항상 정답(풍력은 정답 방향, 수력은 문제와 같은 높이)이다.
        /// </summary>
        private string BuildAiResultText(string levelName)
        {
            if (Constants.Levels.IsWind(levelName))
                return BuildWindResultText(_session.lastQuestionTime,
                                           BlockScorer.GetBestDirection(_session.lastQuestionTime, levelName),
                                           true,
                                           Constants.ResultMessages.StatusGood);

            if (Constants.Levels.IsHydro(levelName))
                return BuildHydroResultText(_session.lastQuestionTime,
                                            _session.lastQuestionTime,
                                            true,
                                            Constants.ResultMessages.StatusGood);

            if (Constants.Levels.IsPowerPlant(levelName))
                return BuildPowerPlantResultText(Constants.ResultMessages.PowerPlantBestCondition,
                                                 true, true, Constants.ResultMessages.StatusGood);

            return BuildResultText(BlockScorer.GetBestCount(),
                                   BlockScorer.GetBestDirection(_session.lastQuestionTime, levelName),
                                   Constants.ResultMessages.StatusGood);
        }

        /// <summary>
        /// 플레이어 결과 → AI 결과 → 확인 버튼 → 완료 패널 순서의 결과 연출 전체를 진행한다.
        /// </summary>
        private async UniTaskVoid PlaySequence()
        {
            CancellationToken ct = destroyCancellationToken;
            try
            {
                // 확인 버튼이 열리기 전까지는 입력 없이 연출만 보는 구간 — 비활동 타이머를 멈춘다
                if (_inactivityTimer) _inactivityTimer.Pause();

                await LoadSceneSettingsAsync(ct);
                _fadeDuration = await SceneFader.GetPanelFadeDurationAsync();

                if (playerText) await playerText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(playerImageGroup, 0f, 1f, _fadeDuration, ct);
                await PlayPlayerStageAsync(ct);
                await PlayEfficiencyAsync(playerEffGroup, playerEffText, _playerPercent, ct);
                await FadeToGrayscaleAsync(ct);

                await PlayAiStartAsync(ct);

                // AI 시작 안내가 사라진 뒤 AI 결과 패널 페이드인 → 연출 시작
                if (aiResultGroup)
                    await SceneFader.FadeCanvasGroupAsync(aiResultGroup, 0f, 1f, _fadeDuration, ct);

                if (aiText) await aiText.PlayAsync(ct);
                await SceneFader.FadeCanvasGroupAsync(aiImageGroup, 0f, 1f, _fadeDuration, ct);
                await PlayAiStageAsync(ct);
                await PlayEfficiencyAsync(aiEffGroup, aiEffText, MaxPercent, ct);

                await SceneFader.FadeCanvasGroupAsync(confirmButtonGroup, 0f, 1f, _fadeDuration, ct);
                SceneFader.SetGroupInteractable(confirmButtonGroup, true);

                // 확인 버튼이 열렸으니 이제부터는 사용자 입력을 기다리는 구간 — 타이머 재개
                if (_inactivityTimer) _inactivityTimer.Resume();

                if (!confirmButton) return;
                await confirmButton.OnClickAsync(ct);
                // 페이드 중 재클릭 방지
                SceneFader.SetGroupInteractable(confirmButtonGroup, false);

                // 순차 페이드 — resultPanel이 완전히 꺼진 뒤 completePanel이 켜짐
                SceneFader.SetGroupInteractable(resultPanel, false);
                await SceneFader.FadeCanvasGroupAsync(resultPanel, 1f, 0f, _fadeDuration, ct);
                SceneFader.SetGroupInteractable(completePanel, true);
                await SceneFader.FadeCanvasGroupAsync(completePanel, 0f, 1f, _fadeDuration, ct);
            }
            catch (OperationCanceledException)
            {
                // 시퀀스 도중 씬 전환(다음 버튼 등)으로 오브젝트가 파괴된 경우 — 정상 종료.
                // 확인 버튼이 열리기 전에 빠져나갔다면 타이머가 멈춘 채 남으므로 여기서 되돌린다.
                if (_inactivityTimer) _inactivityTimer.Resume();
            }
        }

        /// <summary>
        /// 플레이어 스테이지 연출을 재생한다 — 태양광은 패널 방향, 풍력은 풍차 회전 속도,
        /// 수력은 댐 수문 개방량, 발전소는 피스톤·수증기 강도.
        /// 셋 다 에너지 효율(%)을 연출 강도로 쓴다(발전소만 부족 구간을 정지로 잘라낸다).
        /// </summary>
        private async UniTask PlayPlayerStageAsync(CancellationToken ct)
        {
            if (_isWindStage)
            {
                if (playerTurbineSpin) await playerTurbineSpin.ApplyAsync(_playerPercent, ct);
                else WarnMissingStage(nameof(playerTurbineSpin));
                return;
            }

            if (_isHydroStage)
            {
                if (playerDamGates) await playerDamGates.ApplyAsync(_playerPercent, ct);
                else WarnMissingStage(nameof(playerDamGates));
                return;
            }

            if (_isPlantStage)
            {
                if (playerPlantPump) await playerPlantPump.ApplyAsync(_playerPercent, ct);
                else WarnMissingStage(nameof(playerPlantPump));
                return;
            }

            if (playerPanelPose)
                await playerPanelPose.ApplyAsync(null, _session ? _session.lastDirection : null, ct);
            else
                WarnMissingStage(nameof(playerPanelPose));
        }

        /// <summary>
        /// AI 스테이지 연출을 재생한다 — AI는 항상 정답이므로 전부 100% 기준으로 재생한다.
        /// </summary>
        private async UniTask PlayAiStageAsync(CancellationToken ct)
        {
            if (_isWindStage)
            {
                if (aiTurbineSpin) await aiTurbineSpin.ApplyAsync(MaxPercent, ct);
                else WarnMissingStage(nameof(aiTurbineSpin));
                return;
            }

            if (_isHydroStage)
            {
                if (aiDamGates) await aiDamGates.ApplyAsync(MaxPercent, ct);
                else WarnMissingStage(nameof(aiDamGates));
                return;
            }

            if (_isPlantStage)
            {
                if (aiPlantPump) await aiPlantPump.ApplyAsync(MaxPercent, ct);
                else WarnMissingStage(nameof(aiPlantPump));
                return;
            }

            if (aiPanelPose)
                await aiPanelPose.ApplyAsync(null, BlockScorer.GetBestDirection(_session ? _session.lastQuestionTime : null, CurrentLevelName), ct);
            else
                WarnMissingStage(nameof(aiPanelPose));
        }

        /// <summary>
        /// 스테이지 연출 컴포넌트가 할당되지 않아 연출을 건너뛴다는 경고를 남긴다.
        /// </summary>
        private void WarnMissingStage(string fieldName)
        {
            if (_logger != null) _logger.ZLogWarning($"[ResultSequence] {fieldName}이(가) 할당되지 않아 스테이지 연출을 건너뜁니다.");
        }

        /// <summary>
        /// AI 코딩 시작 안내 패널을 보여준다 — 페이드인 → 점(0~3) 반복 애니메이션 → 약 aiStartHold초 후 페이드아웃.
        /// </summary>
        private async UniTask PlayAiStartAsync(CancellationToken ct)
        {
            if (!aiStartPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] aiStartPanel이 할당되지 않아 AI 시작 안내를 건너뜁니다.");
                return;
            }

            using CancellationTokenSource dotCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            AnimateDotsAsync(dotCts.Token).Forget();

            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 0f, 1f, _fadeDuration, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(_sceneSettings?.aiStartHold ?? aiStartHold), cancellationToken: ct);
            await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 1f, 0f, _fadeDuration, ct);

            dotCts.Cancel();
        }

        /// <summary>
        /// 'AI가 코딩을 시작합니다' 뒤 점 개수를 0→3 반복한다 (취소될 때까지).
        /// 상단 문구와 같은 방식 — 문자열은 점 3개를 포함한 채로 두고 노출 개수만 바꿔 문구가 좌우로 흔들리지 않게 한다.
        /// </summary>
        private async UniTaskVoid AnimateDotsAsync(CancellationToken ct)
        {
            if (!aiStartText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] aiStartText가 할당되지 않아 점 애니메이션을 건너뜁니다.");
                return;
            }

            aiStartText.text = ZString.Concat(Constants.ResultMessages.AiCodingStart, Constants.ResultMessages.AiCodingDots);
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

        /// <summary>
        /// 4_Result.json을 로드하고, 타이프라이터·패널 자세처럼 다른 컴포넌트가 가진 값도 여기서 밀어넣는다.
        /// </summary>
        private async UniTask LoadSceneSettingsAsync(CancellationToken ct)
        {
            string path = ZString.Concat(Constants.ResourcePaths.SceneSettingsFolder, "/", Constants.Scenes.Result);
            _sceneSettings = await JsonLoader.LoadAsync<ResultSceneSettings>(path, ct, _logger);
            if (_sceneSettings is null) return;

            if (playerText) playerText.CharInterval = _sceneSettings.typewriterCharInterval;
            if (aiText) aiText.CharInterval = _sceneSettings.typewriterCharInterval;
            if (playerPanelPose) playerPanelPose.AnimDuration = _sceneSettings.panelPoseDuration;
            if (aiPanelPose) aiPanelPose.AnimDuration = _sceneSettings.panelPoseDuration;
            if (playerTurbineSpin) playerTurbineSpin.RampDuration = _sceneSettings.turbineSpinDuration;
            if (aiTurbineSpin) aiTurbineSpin.RampDuration = _sceneSettings.turbineSpinDuration;
            if (playerDamGates) playerDamGates.OpenDuration = _sceneSettings.damOpenDuration;
            if (aiDamGates) aiDamGates.OpenDuration = _sceneSettings.damOpenDuration;
            if (playerPlantPump) playerPlantPump.RampDuration = _sceneSettings.plantPumpDuration;
            if (aiPlantPump) aiPlantPump.RampDuration = _sceneSettings.plantPumpDuration;
        }

        /// <summary>
        /// 상단 안내 문구에 레벨별 문구와 말줄임 슬롯을 붙인다.
        /// 문구가 비어 있는 레벨(미정)은 씬에 입력해둔 텍스트를 그대로 쓴다.
        /// </summary>
        private void ApplyTopText()
        {
            if (!topText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] topText가 할당되지 않아 상단 안내 문구를 표시하지 않습니다.");
                return;
            }

            string baseText = _session && _session.currentLevel ? _session.currentLevel.resultTopText : null;
            if (string.IsNullOrEmpty(baseText))
                baseText = topText.text;

            // 씬 텍스트에 이미 말줄임이 들어 있으면 중복되지 않도록 떼어낸다
            if (baseText.EndsWith(Constants.ResultMessages.ResultTopDots))
                baseText = baseText[..^Constants.ResultMessages.ResultTopDots.Length];

            topText.text = ZString.Concat(baseText, Constants.ResultMessages.ResultTopDots);
            topText.ForceMeshUpdate();
        }

        /// <summary>
        /// 상단 문구 뒤 점 개수를 0→3 반복한다 (씬이 끝날 때까지).
        /// 문자열은 말줄임을 포함한 채로 두고 노출 개수만 바꾸므로 문구 폭이 고정되어 좌우로 흔들리지 않는다.
        /// </summary>
        private async UniTaskVoid AnimateTopDotsAsync(CancellationToken ct)
        {
            // topText 누락은 ApplyTopText에서 이미 경고했다
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

        /// <summary>
        /// 에너지 효율 텍스트를 페이드인한 뒤 0%에서 target%까지 카운트업한다.
        /// </summary>
        private async UniTask PlayEfficiencyAsync(CanvasGroup group, TextMeshProUGUI text, int target, CancellationToken ct)
        {
            if (!group || !text)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] 효율 표시 그룹 또는 텍스트가 할당되지 않아 카운트업을 건너뜁니다.");
                return;
            }

            text.text = FormatEfficiency(0);
            await SceneFader.FadeCanvasGroupAsync(group, 0f, 1f, _fadeDuration, ct);

            // 빠르게 오르다 끝에서 감속하는 카운터 연출 (순수 수치 보간이므로 DOVirtual)
            await DOVirtual.Float(0f, target, _sceneSettings?.effCountDuration ?? effCountDuration, v =>
                {
                    text.text = FormatEfficiency(Mathf.Clamp(Mathf.RoundToInt(v), 0, target));
                })
                .SetEase(Ease.OutQuad)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
            text.text = FormatEfficiency(target);
        }

        /// <summary>
        /// 효율 퍼센트를 표시 문자열로 만든다.
        /// </summary>
        private static string FormatEfficiency(int percent)
            => ZString.Format(Constants.ResultMessages.EfficiencyFormat, percent);
    }
}
