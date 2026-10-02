using System;
using System.Collections.Generic;
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
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace Scenes
{
    public class ResultSequence : MonoBehaviour
    {
        [SerializeField] private ResultRowsView playerRows;
        [SerializeField] private CanvasGroup playerImageGroup;
        [SerializeField] private ResultRowsView aiRows;
        [SerializeField] private CanvasGroup aiImageGroup;
        [SerializeField] private CanvasGroup aiResultGroup;
        [SerializeField] private CanvasGroup playerEffGroup;
        [SerializeField] private TextMeshProUGUI playerEffText;
        [SerializeField] private CanvasGroup aiEffGroup;
        [SerializeField] private TextMeshProUGUI aiEffText;
        // 레벨별 3D 결과 스테이지 — 모델과 전용 카메라(RenderTexture 공유)를 통째로 켜고 끈다.
        // 모든 스테이지의 카메라가 같은 RT를 노리므로 반드시 한 스테이지만 활성 상태여야 한다.
        // 레벨1은 태양광 스테이지, 레벨5는 연구소 스테이지를 쓴다.
        [SerializeField] private GameObject solarStage;
        [SerializeField] private GameObject windStage;
        [SerializeField] private GameObject hydroStage;
        [SerializeField] private GameObject plantStage;
        [SerializeField] private GameObject labStage;
        [SerializeField] private SolarPanelModelPose playerPanelPose;
        [SerializeField] private SolarPanelModelPose aiPanelPose;
        [SerializeField] private WindTurbineSpin playerTurbineSpin;
        [SerializeField] private WindTurbineSpin aiTurbineSpin;
        [SerializeField] private DamGateFlow playerDamGates;
        [SerializeField] private DamGateFlow aiDamGates;
        [SerializeField] private PowerPlantPump playerPlantPump;
        [SerializeField] private PowerPlantPump aiPlantPump;
        [SerializeField] private LabLightGlow playerLabGlow;
        [SerializeField] private LabLightGlow aiLabGlow;
        [SerializeField] private float effCountDuration = 0.8f;

        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private CanvasGroup completePanel;
        [Tooltip("완료 패널 제목 텍스트 — 결과에 따라 '미션 성공!' / '미션 실패!'로 바뀐다")]
        [SerializeField] private TMP_Text completeTitleText;
        [SerializeField] private Button nextButton;
        [Tooltip("결과 연출이 끝나면 하단 중앙에 띄우는 '화면을 터치하면 다음으로 넘어갑니다' 안내")]
        [SerializeField] private CanvasGroup touchGuideGroup;
        [SerializeField] private float touchGuideBlinkMinAlpha = 0.35f;
        [SerializeField] private float touchGuideBlinkDuration = 0.8f;
        [SerializeField] private float touchGuideDelay = 1f;
        [SerializeField] private CanvasGroup aiStartPanel;
        [SerializeField] private TMP_Text aiStartText;
        [SerializeField] private float aiStartHold = 3f;
        [SerializeField] private TMP_Text topText;

        private GameSession _session;
        private InactivityTimer _inactivityTimer;
        private ILogger<ResultSequence> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 게임 세션, 비활동 타이머, 로거, 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(GameSession session, InactivityTimer inactivityTimer, ILogger<ResultSequence> logger,
            SoundManager soundManager)
        {
            _session = session;
            _inactivityTimer = inactivityTimer;
            _logger = logger;
            _soundManager = soundManager;
        }

        private const int MaxPercent = 100;

        private LevelKind CurrentLevelKind => _session && _session.currentLevel ? _session.currentLevel.kind : LevelKind.Solar;

        // 이번 판 문제의 정답 방향 — AI 결과 행과 AI 패널 연출에 쓴다 (정답 방향이 없는 레벨은 null)
        private string CurrentCorrectAnswer =>
            _session && _session.currentLevel ? _session.currentLevel.GetCorrectAnswer(_session.lastQuestionTime) : null;

        // 셰이더 프로퍼티 조회 비용을 줄이기 위한 ID 캐시
        private readonly static int GrayscaleAmountId = Shader.PropertyToID("_GrayscaleAmount");

        private int _playerPercent;
        private bool _isWindStage;             // 풍력 스테이지로 연출 중인지 (레벨2)
        private bool _isHydroStage;            // 수력 스테이지로 연출 중인지 (레벨3)
        private bool _isPlantStage;            // 발전소 스테이지로 연출 중인지 (레벨4)
        private bool _isLabStage;              // 연구소 스테이지로 연출 중인지 (레벨5)
        private Material _grayscaleInstance;   // 흑백 전환용 머티리얼 인스턴스 (null이면 컬러 유지)
        private Tween _touchGuideBlinkTween;   // 터치 안내 깜빡임 — 무한 반복이라 직접 Kill한다
        private string _missionResultSound;    // 완료 패널(미션 성공/실패)이 뜰 때 낼 효과음 — 게임 결과 없이 진입하면 null

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

            if (!playerRows) _logger.ZLogWarning($"[ResultSequence] playerRows가 할당되지 않았습니다.");
            if (!aiRows) _logger.ZLogWarning($"[ResultSequence] aiRows가 할당되지 않았습니다.");
            if (!playerImageGroup) _logger.ZLogWarning($"[ResultSequence] playerImageGroup이 할당되지 않았습니다.");
            if (!touchGuideGroup) _logger.ZLogWarning($"[ResultSequence] touchGuideGroup이 할당되지 않았습니다.");
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
            // (플레이어 이미지 → 효율 → AI 시작 안내 → AI 결과/이미지/효율 → 터치 안내)
            SceneFader.InitializePanelState(playerImageGroup, false);
            SceneFader.InitializePanelState(playerEffGroup, false);
            SceneFader.InitializePanelState(aiStartPanel, false);
            SceneFader.InitializePanelState(aiResultGroup, false);
            SceneFader.InitializePanelState(aiImageGroup, false);
            SceneFader.InitializePanelState(aiEffGroup, false);
            SceneFader.InitializePanelState(touchGuideGroup, false);

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
            if (playerLabGlow) playerLabGlow.SetNeutral();
            if (aiLabGlow) aiLabGlow.SetNeutral();
        }

        /// <summary>
        /// 흑백 머티리얼 인스턴스를 정리하고, 멈춰 있을 수 있는 비활동 타이머를 재개한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_touchGuideBlinkTween != null && _touchGuideBlinkTween.IsActive()) _touchGuideBlinkTween.Kill();

            // 흑백 전환용 머티리얼은 런타임에 new로 만든 인스턴스라 씬 언로드로 회수되지 않는다
            if (_grayscaleInstance)
            {
                Destroy(_grayscaleInstance);
                _grayscaleInstance = null;
            }

            if (nextButton)
                nextButton.onClick.RemoveListener(OnNextClicked);

            // 터치 안내가 뜨기 전에 파괴됐다면 타이머가 멈춘 채 남는다 —
            // 이미 재개된 상태에서 다시 불러도 카운트만 처음부터 다시 시작할 뿐 부작용이 없다
            if (_inactivityTimer) _inactivityTimer.Resume();
        }

        /// <summary>
        /// 다음 레벨로 진행한다 — 방금 플레이한 레벨의 AfterResultScene을 따라간다 (마지막 레벨은 5_Outro).
        /// </summary>
        private void OnNextClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            string nextScene = _session && _session.currentLevel ? _session.currentLevel.AfterResultScene : Constants.Scenes.Story;

            // 미션 성공·실패와 상관없이 다음 레벨을 연다(기획 확인, 2026-10-02).
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
        /// 발전소(레벨4)는 발전소 건물, 미래에너지(레벨5)는 연구소, 나머지는 태양광 패널.
        /// </summary>
        private void ApplyLevelStage()
        {
            LevelKind kind = CurrentLevelKind;
            _isWindStage = kind == LevelKind.Wind;
            _isHydroStage = kind == LevelKind.Hydro;
            _isPlantStage = kind == LevelKind.PowerPlant;
            _isLabStage = kind == LevelKind.FutureEnergy;

            if (solarStage) solarStage.SetActive(!_isWindStage && !_isHydroStage && !_isPlantStage && !_isLabStage);
            if (windStage) windStage.SetActive(_isWindStage);
            if (hydroStage) hydroStage.SetActive(_isHydroStage);
            if (plantStage) plantStage.SetActive(_isPlantStage);
            if (labStage) labStage.SetActive(_isLabStage);
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

            LevelKind kind = CurrentLevelKind;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시.
            // 레벨마다 채워지는 값이 달라 개별 필드로 판정하지 않고 게임 씬이 세운 플래그를 그대로 쓴다.
            bool hasCoding = _session.hasCodingResult;
            List<ResultRow> playerResult;
            bool isSuccess = false;

            if (hasCoding)
            {
                // 최고 점수 대비 비율로 전력 수급 상태 판정
                int maxScore = BlockScorer.GetMaxScore(kind);
                float percent = maxScore > 0 ? _session.lastScore * 100f / maxScore : 0f;
                string status = ToStatusText(percent);
                _playerPercent = Mathf.Clamp(Mathf.FloorToInt(percent), 0, MaxPercent);

                playerResult = BuildPlayerRows(kind, status);
                isSuccess = status != Constants.ResultMessages.StatusPoor;

                // 전력이 부족한 결과는 흑백으로 전환해 시각적으로 구분
                if (status == Constants.ResultMessages.StatusPoor)
                    ApplyGrayscale();
            }
            else
            {
                // 스킵/코딩 미완료 — 효율 0% 고정
                _playerPercent = 0;
                // 체험자가 정한 값은 '-', 감지 항목은 OFF, 발전이 일어나지 않았으니 전력 수급 상태는 '부족'.
                // 문제로 주어진 값(바람 방향·발전소 상황)은 그대로 보여준다.
                string poor = Constants.ResultMessages.StatusPoor;
                playerResult =
                    kind == LevelKind.Wind       ? BuildWindRows(_session.lastQuestionTime, null, false, poor) :
                    kind == LevelKind.Hydro      ? BuildHydroRows(null, false, Constants.ResultMessages.NoValue, poor) :
                    kind == LevelKind.PowerPlant ? BuildPowerPlantRows(null, Constants.ResultMessages.NoValue, Constants.ResultMessages.NoValue, poor) :
                    kind == LevelKind.FutureEnergy ? BuildFutureEnergyRows(false, null, poor) :
                                                   BuildSolarRows(null, null, poor);
                ApplyGrayscale();
            }

            if (playerRows) playerRows.SetRows(playerResult);
            if (aiRows) aiRows.SetRows(BuildAiRows(kind));

            _missionResultSound = isSuccess ? Constants.Sounds.MissionSuccess : Constants.Sounds.MissionFailed;

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
        /// 값이 비었으면 '-'로 바꾼다 (코딩을 건너뛰었거나 값을 읽지 못한 경우).
        /// </summary>
        private static string OrNoValue(string value)
            => string.IsNullOrEmpty(value) ? Constants.ResultMessages.NoValue : value;

        /// <summary>
        /// 감지 여부를 ON/OFF 표기로 바꾼다.
        /// </summary>
        private static string OnOff(bool isOn)
            => isOn ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff;

        /// <summary>
        /// 레벨1(태양광) 결과 행 — 설치 개수 / 패널 방향 / 전력 수급 상태.
        /// </summary>
        private static List<ResultRow> BuildSolarRows(string count, string direction, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelCount, OrNoValue(count)),
            new ResultRow(Constants.ResultMessages.LabelDirection, OrNoValue(direction)),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨2(풍력) 결과 행 — 문제로 나온 바람 방향 / 플레이어가 맞춘 풍차 방향 / 반복하기 사용 여부.
        /// 반복하기 없이는 컴파일이 막히므로 정상 플레이에서 반복 감지는 항상 ON이다.
        /// </summary>
        private static List<ResultRow> BuildWindRows(string windDirection, string bladeDirection, bool repeatUsed, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelWindDirection, OrNoValue(windDirection)),
            new ResultRow(Constants.ResultMessages.LabelBladeDirection, OrNoValue(bladeDirection)),
            new ResultRow(Constants.ResultMessages.LabelRepeat, OnOff(repeatUsed)),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨3(수력) 결과 행 — 플레이어가 만약 블록에 연결한 수문 개방 높이 / 조건·아니면 사용 여부 / 수문 열기·닫기 순서.
        /// 조건 감지는 조건 블록 연결 여부 — 조건 없이는 컴파일이 막히므로 정상 플레이에선 항상 ON이다.
        /// 아니면과 수문 열기·닫기 순서는 채점 항목이라 틀렸을 때 AI 결과와 달라 보이도록 따로 표시한다.
        /// 코딩을 건너뛰면 수문 순서는 판단할 배치가 없으므로 정상/오류 대신 '-'로 둔다.
        /// </summary>
        private static List<ResultRow> BuildHydroRows(string gateHeight, bool elseUsed, string gateOrder, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelGateHeight, OrNoValue(gateHeight)),
            new ResultRow(Constants.ResultMessages.LabelConditionOn, OnOff(!string.IsNullOrEmpty(gateHeight))),
            new ResultRow(Constants.ResultMessages.LabelElse, OnOff(elseUsed)),
            new ResultRow(Constants.ResultMessages.LabelGateOrder, gateOrder),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 수문 열기·닫기 순서가 맞았는지를 정상/오류 표기로 바꾼다.
        /// </summary>
        private static string GateOrderText(bool isCorrect)
            => isCorrect ? Constants.ResultMessages.GateOrderCorrect : Constants.ResultMessages.GateOrderWrong;

        /// <summary>
        /// 레벨4(발전소) 결과 행 — 고정 상황 / 플레이어가 만약에 연결한 조건식 / 놀이시설 끄기 조건(만약)·병원 전력 유지.
        /// 뒤 두 항목은 놀이시설·병원 채점과 같은 기준이라 효율 %가 왜 그렇게 나왔는지 화면에서 읽힌다.
        /// 코딩을 건너뛰면 두 항목은 판단할 배치가 없으므로 ON/OFF 대신 '-'로 둔다.
        /// </summary>
        private static List<ResultRow> BuildPowerPlantRows(string condition, string amusementPowerCut, string hospitalPowerKept, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelSituation, Constants.ResultMessages.PowerPlantSituation),
            new ResultRow(Constants.ResultMessages.LabelCondition, OrNoValue(condition)),
            new ResultRow(Constants.ResultMessages.LabelAmusement, amusementPowerCut),
            new ResultRow(Constants.ResultMessages.LabelHospital, hospitalPowerKept),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨5(미래에너지) 결과 행 — 함수 사용 여부 / 에너지 블록별로 함수 안에 넣었는지 / 전력 수급 상태.
        /// 에너지 행은 채점(함수 안 에너지 수)과 같은 기준이라 효율 %가 왜 그렇게 나왔는지 화면에서 읽힌다.
        /// </summary>
        private static List<ResultRow> BuildFutureEnergyRows(bool functionUsed, ICollection<string> energiesInFunction, string status)
        {
            List<ResultRow> rows = new() { new ResultRow(Constants.ResultMessages.LabelFunctionUsed, OnOff(functionUsed)) };
            foreach (string energy in BlockScorer.FutureEnergyNames)
                rows.Add(new ResultRow(energy, OnOff(energiesInFunction is not null && energiesInFunction.Contains(energy))));
            rows.Add(new ResultRow(Constants.ResultMessages.LabelStatus, status));
            return rows;
        }

        /// <summary>
        /// 레벨에 맞는 플레이어 결과 행을 세션 값으로 만든다.
        /// </summary>
        private List<ResultRow> BuildPlayerRows(LevelKind kind, string status)
        {
            if (kind == LevelKind.Wind)
                return BuildWindRows(_session.lastQuestionTime, _session.lastDirection, _session.lastRepeatUsed, status);

            if (kind == LevelKind.Hydro)
                return BuildHydroRows(_session.lastGateHeight, _session.lastElseUsed,
                                      GateOrderText(_session.lastGateOrderCorrect), status);

            if (kind == LevelKind.PowerPlant)
                return BuildPowerPlantRows(_session.lastConditionText, OnOff(_session.lastAmusementPowerCut),
                                           OnOff(_session.lastHospitalPowerKept), status);

            if (kind == LevelKind.FutureEnergy)
                return BuildFutureEnergyRows(_session.lastFunctionUsed, _session.lastEnergiesInFunction, status);

            return BuildSolarRows(_session.lastCount, _session.lastDirection, status);
        }

        /// <summary>
        /// AI 결과 행을 만든다 — AI는 항상 정답(풍력은 정답 방향, 수력은 문제와 같은 높이)이다.
        /// </summary>
        private List<ResultRow> BuildAiRows(LevelKind kind)
        {
            string good = Constants.ResultMessages.StatusGood;

            if (kind == LevelKind.Wind)
                return BuildWindRows(_session.lastQuestionTime, CurrentCorrectAnswer, true, good);

            if (kind == LevelKind.Hydro)
                return BuildHydroRows(_session.lastQuestionTime, true, GateOrderText(true), good);

            if (kind == LevelKind.PowerPlant)
                return BuildPowerPlantRows(Constants.ResultMessages.PowerPlantBestCondition, OnOff(true), OnOff(true), good);

            if (kind == LevelKind.FutureEnergy)
                return BuildFutureEnergyRows(true, new List<string>(BlockScorer.FutureEnergyNames), good);

            return BuildSolarRows(BlockScorer.GetBestCount(), CurrentCorrectAnswer, good);
        }

        /// <summary>
        /// 비활동 타이머를 멈춘 채 결과 연출 전체를 진행한다.
        /// </summary>
        private async UniTaskVoid PlaySequence()
        {
            await RunWithTimerPausedAsync(_inactivityTimer, PlaySequenceStepsAsync, destroyCancellationToken, ex =>
            {
                if (_logger != null) _logger.ZLogError(ex, $"[ResultSequence] 결과 연출 중 오류가 발생해 비활동 타이머를 재개합니다.");
            });
        }

        /// <summary>
        /// 비활동 타이머를 멈춘 채 연출을 진행하고, 취소나 오류로 중간에 빠져나가도 타이머를 다시 켠다.
        /// </summary>
        public static async UniTask RunWithTimerPausedAsync(InactivityTimer timer, Func<CancellationToken, UniTask> steps,
            CancellationToken ct, Action<Exception> onError = null)
        {
            try
            {
                // 터치 안내가 뜨기 전까지는 입력 없이 연출만 보는 구간 — 비활동 타이머를 멈춘다
                if (timer) timer.Pause();
                await steps(ct);
            }
            catch (OperationCanceledException)
            {
                // 시퀀스 도중 씬 전환(다음 버튼 등)으로 오브젝트가 파괴된 경우 — 정상 종료.
                // 터치 안내가 뜨기 전에 빠져나갔다면 타이머가 멈춘 채 남으므로 여기서 되돌린다.
                if (timer) timer.Resume();
            }
            catch (Exception ex)
            {
                // 연출 도중 예기치 않은 오류 — 타이머가 멈춘 채 남으면 체험자가 떠나도 타이틀로 돌아가지 않는다
                onError?.Invoke(ex);
                if (timer) timer.Resume();
            }
        }

        /// <summary>
        /// 플레이어 결과 → AI 결과 → 터치 안내 → 완료 패널 순서의 결과 연출 전체를 진행한다.
        /// </summary>
        private async UniTask PlaySequenceStepsAsync(CancellationToken ct)
        {
            await LoadSceneSettingsAsync(ct);
            _fadeDuration = await SceneFader.GetPanelFadeDurationAsync();

            if (playerRows) await playerRows.PlayAsync(ct);
            await SceneFader.FadeCanvasGroupAsync(playerImageGroup, 0f, 1f, _fadeDuration, ct);
            await PlayPlayerStageAsync(ct);
            await PlayEfficiencyAsync(playerEffGroup, playerEffText, _playerPercent, ct);
            await FadeToGrayscaleAsync(ct);

            await PlayAiStartAsync(ct);

            // AI 시작 안내가 사라진 뒤 AI 결과 패널 페이드인 → 연출 시작
            if (aiResultGroup)
                await SceneFader.FadeCanvasGroupAsync(aiResultGroup, 0f, 1f, _fadeDuration, ct);

            if (aiRows) await aiRows.PlayAsync(ct);
            await SceneFader.FadeCanvasGroupAsync(aiImageGroup, 0f, 1f, _fadeDuration, ct);
            await PlayAiStageAsync(ct);
            await PlayEfficiencyAsync(aiEffGroup, aiEffText, MaxPercent, ct);

            // AI 결과를 읽을 틈을 준 뒤 안내를 띄운다
            float guideDelay = _sceneSettings?.touchGuideDelay ?? touchGuideDelay;
            await UniTask.Delay(TimeSpan.FromSeconds(guideDelay), cancellationToken: ct);
            await ShowTouchGuideAsync(ct);

            // 안내가 떴으니 이제부터는 사용자 입력을 기다리는 구간 — 타이머 재개
            if (_inactivityTimer) _inactivityTimer.Resume();

            // 화면 아무 곳이나 새로 누르면 완료 화면으로 넘어간다
            await UniTask.WaitUntil(StoryLineAnimator.IsPointerPressedThisFrame, cancellationToken: ct);
            HideTouchGuide();

            // 순차 페이드 — resultPanel이 완전히 꺼진 뒤 completePanel이 켜짐
            SceneFader.SetGroupInteractable(resultPanel, false);
            await SceneFader.FadeCanvasGroupAsync(resultPanel, 1f, 0f, _fadeDuration, ct);
            SceneFader.SetGroupInteractable(completePanel, true);
            if (_soundManager && _missionResultSound != null) _soundManager.PlaySFX(_missionResultSound);
            await SceneFader.FadeCanvasGroupAsync(completePanel, 0f, 1f, _fadeDuration, ct);
        }

        /// <summary>
        /// 플레이어 스테이지 연출을 재생한다 — 태양광은 패널 방향, 풍력은 풍차 회전 속도,
        /// 수력은 댐 수문 개방량, 발전소는 피스톤·수증기 강도, 연구소는 건물 안 조명.
        /// 모두 에너지 효율(%)을 연출 강도로 쓴다(발전소는 부족 구간을 정지로 잘라내고,
        /// 연구소는 부족 꺼짐·보통 약하게 깜빡임·양호 강하게로 단계를 나눈다).
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

            if (_isLabStage)
            {
                if (playerLabGlow) await playerLabGlow.ApplyAsync(_playerPercent, ct);
                else WarnMissingStage(nameof(playerLabGlow));
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

            if (_isLabStage)
            {
                if (aiLabGlow) await aiLabGlow.ApplyAsync(MaxPercent, ct);
                else WarnMissingStage(nameof(aiLabGlow));
                return;
            }

            if (aiPanelPose)
                await aiPanelPose.ApplyAsync(null, CurrentCorrectAnswer, ct);
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

            if (playerRows)
            {
                playerRows.CharInterval = _sceneSettings.typewriterCharInterval;
                playerRows.RowFadeDuration = _sceneSettings.rowFadeDuration;
            }
            if (aiRows)
            {
                aiRows.CharInterval = _sceneSettings.typewriterCharInterval;
                aiRows.RowFadeDuration = _sceneSettings.rowFadeDuration;
            }
            if (playerPanelPose) playerPanelPose.AnimDuration = _sceneSettings.panelPoseDuration;
            if (aiPanelPose) aiPanelPose.AnimDuration = _sceneSettings.panelPoseDuration;
            if (playerTurbineSpin) playerTurbineSpin.RampDuration = _sceneSettings.turbineSpinDuration;
            if (aiTurbineSpin) aiTurbineSpin.RampDuration = _sceneSettings.turbineSpinDuration;
            if (playerDamGates) playerDamGates.OpenDuration = _sceneSettings.damOpenDuration;
            if (aiDamGates) aiDamGates.OpenDuration = _sceneSettings.damOpenDuration;
            if (playerPlantPump) playerPlantPump.RampDuration = _sceneSettings.plantPumpDuration;
            if (aiPlantPump) aiPlantPump.RampDuration = _sceneSettings.plantPumpDuration;
            if (playerLabGlow) playerLabGlow.RampDuration = _sceneSettings.labLightDuration;
            if (aiLabGlow) aiLabGlow.RampDuration = _sceneSettings.labLightDuration;
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
        /// 하단 중앙의 '화면을 터치하면 다음으로 넘어갑니다' 안내를 띄우고 천천히 깜빡이게 한다.
        /// </summary>
        private async UniTask ShowTouchGuideAsync(CancellationToken ct)
        {
            if (!touchGuideGroup) return;

            await SceneFader.FadeCanvasGroupAsync(touchGuideGroup, 0f, 1f, _fadeDuration, ct);
            _touchGuideBlinkTween = touchGuideGroup.DOFade(touchGuideBlinkMinAlpha, touchGuideBlinkDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(touchGuideGroup.gameObject);
        }

        /// <summary>
        /// 터치 안내의 깜빡임을 멈추고 숨긴다.
        /// </summary>
        private void HideTouchGuide()
        {
            if (_touchGuideBlinkTween != null && _touchGuideBlinkTween.IsActive()) _touchGuideBlinkTween.Kill();
            if (touchGuideGroup) touchGuideGroup.alpha = 0f;
        }

        /// <summary>
        /// 효율 퍼센트를 표시 문자열로 만든다.
        /// </summary>
        private static string FormatEfficiency(int percent)
            => ZString.Format(Constants.ResultMessages.EfficiencyFormat, percent);
    }
}
