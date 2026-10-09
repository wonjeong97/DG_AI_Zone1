using System;
using System.Threading;
using App;
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
using Network;
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
        private VisitorInfoProvider _visitorInfoProvider;
        private VisitorApiClient _visitorApiClient;

        /// <summary>
        /// 게임 세션, 비활동 타이머, 로거, 사운드 매니저, 체험자 정보 제공자, 체험자 서버 API를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(GameSession session, InactivityTimer inactivityTimer, ILogger<ResultSequence> logger,
            SoundManager soundManager, VisitorInfoProvider visitorInfoProvider, VisitorApiClient visitorApiClient)
        {
            _session = session;
            _inactivityTimer = inactivityTimer;
            _logger = logger;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
            _visitorApiClient = visitorApiClient;
        }

        private const int MaxPercent = Constants.ResultMessages.MaxPercent;

        private LevelKind CurrentLevelKind => _session != null && _session.currentLevel ? _session.currentLevel.kind : LevelKind.Solar;

        // 이번 판 문제의 정답 방향 — AI 결과 행과 AI 패널 연출에 쓴다 (정답 방향이 없는 레벨은 null)
        private string CurrentCorrectAnswer =>
            _session != null && _session.currentLevel ? _session.currentLevel.GetCorrectAnswer(_session.lastQuestionTime) : null;

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
            AnimateTopDots(destroyCancellationToken);

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
            if (!resultPanel) _logger.ZLogWarning($"[ResultSequence] resultPanel이 할당되지 않았습니다.");
            if (!completePanel) _logger.ZLogWarning($"[ResultSequence] completePanel이 할당되지 않아 다음 버튼이 있는 완료 화면을 띄울 수 없습니다.");
            if (!aiResultGroup) _logger.ZLogWarning($"[ResultSequence] aiResultGroup이 할당되지 않았습니다.");
            if (!aiImageGroup) _logger.ZLogWarning($"[ResultSequence] aiImageGroup이 할당되지 않았습니다.");
        }

        /// <summary>
        /// 시퀀스가 건드리는 모든 표시 상태를 첫 입장 기준으로 되돌린다.
        /// </summary>
        private void InitializeSceneState()
        {
            // 에디터에서 연출 중간 값(alpha, 꺼둔 패널, 흑백 머티리얼 등)을 저장해두고 플레이해도
            // 항상 같은 그림에서 연출이 시작되도록, 씬에 저장된 값에 의존하지 않는 것이 목적이다.
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
            // 무한 반복 깜빡임이라 직접 멈춘다
            HideTouchGuide();

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
            // 관리자 레벨 이동으로 시작한 판이면 타이틀의 관리자 화면으로 돌아간다.
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            string nextScene = _session != null && _session.currentLevel ? _session.currentLevel.AfterResultScene : Constants.Scenes.Story;

            if (_session != null && _session.isAdminLevelJump)
            {
                _session.openAdminOnTitle = true;
                nextScene = Constants.Scenes.Title;
            }

            // 미션 성공·실패와 상관없이 다음 레벨을 연다(기획 확인, 2026-10-02).
            // 이미 해금된 이전 레벨을 다시 플레이한 경우엔 진행도를 건드리지 않는다.
            // 무조건 +1 하면 재플레이만으로 아직 깨지 않은 레벨까지 해금돼버린다.
            if (_session != null && _session.currentLevel)
                _session.unlockedLevelIndex = Mathf.Max(_session.unlockedLevelIndex, _session.currentLevel.levelIndex + 1);
            else if (_logger != null)
                _logger.ZLogWarning($"[ResultSequence] 현재 레벨 정보가 없어 진행도를 갱신하지 않고 {nextScene}(으)로 이동합니다.");

            if (_logger != null) _logger.ZLogInformation($"[ResultSequence] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 결과 화면에서 다음을 누름 — {nextScene}(으)로 이동함.");
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
            // 문제 값은 레벨 데이터에 후보가 없으면 비어 있을 수 있어, 게임 씬을 거쳤는지는 레벨로 판단한다
            if (_session == null || !_session.currentLevel)
            {
                if (_logger != null) _logger.ZLogInformation($"[ResultSequence] 게임 씬 결과가 없어 씬 기본 텍스트를 그대로 표시합니다.");
                return;
            }

            LevelKind kind = CurrentLevelKind;

            // 코딩 완료(컴파일 성공) 없이 넘어온 경우 값 대신 '-' 표시.
            // 레벨마다 채워지는 값이 달라 개별 필드로 판정하지 않고 게임 씬이 세운 플래그를 그대로 쓴다.
            // 넘어가기·코딩 미완료는 발전이 일어나지 않았으니 효율 0%, 전력 수급 상태는 '부족'이다
            bool hasCoding = _session.hasCodingResult;
            int maxScore = BlockScorer.GetMaxScore(kind);
            float percent = hasCoding && maxScore > 0 ? _session.lastScore * (float)MaxPercent / maxScore : 0f;
            _playerPercent = Mathf.Clamp(Mathf.FloorToInt(percent), 0, MaxPercent);

            // 전력 수급 상태가 '부족'(보통 기준 미만)이면 미션 실패 — 실패한 결과는 흑백으로 전환해 시각적으로 구분
            bool isSuccess = percent >= Constants.ResultMessages.NormalThresholdPercent;
            if (!isSuccess) ApplyGrayscale();

            string status = ResultRowFactory.ToStatusText(percent);
            if (playerRows) playerRows.SetRows(ResultRowFactory.BuildPlayerRows(kind, _session, hasCoding, status));
            if (aiRows) aiRows.SetRows(ResultRowFactory.BuildAiRows(kind, _session.lastQuestionTime, CurrentCorrectAnswer));

            UploadLevelResult(isSuccess);

            _missionResultSound = isSuccess ? Constants.Sounds.MissionSuccess : Constants.Sounds.MissionFailed;

            if (completeTitleText)
                completeTitleText.text = isSuccess ? Constants.ResultMessages.MissionSuccess : Constants.ResultMessages.MissionFail;
            else if (_logger != null)
                _logger.ZLogWarning($"[ResultSequence] completeTitleText가 할당되지 않아 미션 성공/실패를 표시하지 못했습니다.");
        }

        /// <summary>
        /// 서버 모드에서 QR로 확인한 체험자면 이번 레벨의 미션 결과(성공 1·실패 0, 넘어가기는 실패)를 서버에 올린다.
        /// </summary>
        private void UploadLevelResult(bool isSuccess)
        {
            // 관리자 레벨 이동으로 시작한 판은 체험자 기록에 섞이지 않도록 올리지 않는다.
            // 결과 화면을 빨리 넘겨도 끊기지 않도록 씬 수명과 묶지 않는다 — 요청은 Server.json의 시간 초과·재시도 횟수로 끝난다.
            if (_session == null || _visitorInfoProvider == null || _visitorApiClient == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] 게임 세션·체험자 정보 제공자·서버 API 중 주입되지 않은 것이 있어 레벨 결과를 올리지 않습니다.");
                return;
            }

            // 로컬 모드 — 올릴 서버가 없다
            if (!_visitorInfoProvider.IsServerConnected) return;

            if (_session.isAdminLevelJump)
            {
                if (_logger != null) _logger.ZLogInformation($"[ResultSequence] 관리자 레벨 이동으로 시작한 판이라 레벨 결과를 올리지 않습니다.");
                return;
            }

            if (_visitorInfoProvider.VisitorIdx < 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] QR로 확인한 체험자가 없어 레벨 결과를 올리지 않습니다.");
                return;
            }

            if (!_session.currentLevel)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] 현재 레벨 정보가 없어 레벨 결과를 올리지 않습니다.");
                return;
            }

            string code = VisitorApiClient.GetLevelCode(_session.currentLevel.levelIndex);
            _visitorApiClient.UpdateValueAsync(_visitorInfoProvider.VisitorIdx, _visitorInfoProvider.ServerVisitorName, code, isSuccess,
                CancellationToken.None).Forget();
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
        /// 비활동 타이머를 멈춘 채 결과 연출 전체를 진행한다.
        /// </summary>
        private async UniTaskVoid PlaySequence()
        {
            await RunWithTimerPausedAsync(_inactivityTimer, PlaySequenceStepsAsync, destroyCancellationToken, ex =>
            {
                if (_logger != null) _logger.ZLogError(ex, $"[ResultSequence] 결과 연출 중 오류가 발생해 완료 화면을 바로 띄우고 비활동 타이머를 재개합니다.");
                ShowCompletePanelImmediately();
            });
        }

        /// <summary>
        /// 연출이 오류로 멈췄을 때 결과 패널을 거두고 다음 버튼이 있는 완료 패널을 바로 띄운다.
        /// </summary>
        private void ShowCompletePanelImmediately()
        {
            // 관람객이 결과 화면에 갇히지 않게 터치 안내 단계를 건너뛴다
            HideTouchGuide();
            SceneFader.InitializePanelState(resultPanel, false);
            SceneFader.InitializePanelState(completePanel, true);
        }

        /// <summary>
        /// 비활동 타이머를 멈춘 채 연출을 진행하고, 씬을 떠나지 않은 채 취소나 오류로 빠져나가면 타이머를 다시 켠다.
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
                // 씬을 떠나 취소된 경우에는 다음 씬을 불러올 때 GameManager가 타이머 상태를 정하므로 건드리지 않는다 —
                // 취소는 다음 프레임에 처리돼, 여기서 다시 켜면 타이머를 멈춰 두는 타이틀에서 타이머가 돈다(StoryLineAnimator와 같은 기준).
                // 씬을 떠나지 않았는데 연출이 취소로 끝났다면 타이머가 멈춘 채 남으므로 되돌린다
                if (timer && !ct.IsCancellationRequested) timer.Resume();
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
        /// </summary>
        private async UniTask PlayPlayerStageAsync(CancellationToken ct)
        {
            // 모두 에너지 효율(%)을 연출 강도로 쓴다(발전소는 부족 구간을 정지로 잘라내고,
            // 연구소는 부족 꺼짐·보통 약하게 깜빡임·양호 강하게로 단계를 나눈다).
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
                await playerPanelPose.ApplyAsync(null, _session != null ? _session.lastDirection : null, ct);
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

            // 페이드 도중 취소 외의 예외로 빠져나가도 점 애니메이션이 계속 돌지 않도록 finally에서 멈춘다
            using CancellationTokenSource dotCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                AnimateAiDots(dotCts.Token);

                await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 0f, 1f, _fadeDuration, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(_sceneSettings?.aiStartHold ?? aiStartHold), cancellationToken: ct);
                await SceneFader.FadeCanvasGroupAsync(aiStartPanel, 1f, 0f, _fadeDuration, ct);
            }
            finally
            {
                dotCts.Cancel();
            }
        }

        /// <summary>
        /// 'AI가 코딩을 시작합니다' 뒤 점 개수를 0→3 반복한다 (취소될 때까지).
        /// </summary>
        private void AnimateAiDots(CancellationToken ct)
        {
            if (!aiStartText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] aiStartText가 할당되지 않아 점 애니메이션을 건너뜁니다.");
                return;
            }

            aiStartText.text = ZString.Concat(Constants.ResultMessages.AiCodingStart, Constants.ResultMessages.AiCodingDots);
            aiStartText.ForceMeshUpdate();
            int baseLength = Mathf.Max(0, aiStartText.textInfo.characterCount - Constants.ResultMessages.AiCodingDots.Length);

            CycleDotsAsync(aiStartText, baseLength, 1, Constants.ResultMessages.AiCodingDotCycle - 1,
                () => _sceneSettings?.aiCodingDotIntervalMs ?? Constants.ResultMessages.AiCodingDotIntervalMs, ct).Forget();
        }

        /// <summary>
        /// 문구 뒤 점 슬롯을 0개→slotCount개 순서로 반복해 드러낸다 (취소될 때까지).
        /// </summary>
        private static async UniTaskVoid CycleDotsAsync(TMP_Text text, int baseLength, int slotLength, int slotCount,
            Func<int> getIntervalMs, CancellationToken ct)
        {
            // 문자열은 점을 모두 포함한 채로 두고 노출 개수만 바꾸므로 문구 폭이 고정되어 좌우로 흔들리지 않는다.
            // 간격은 4_Result.json을 늦게 읽어도 반영되도록 매번 다시 읽는다.
            int dotCount = 0;
            try
            {
                while (true)
                {
                    text.maxVisibleCharacters = baseLength + dotCount * slotLength;
                    dotCount = (dotCount + 1) % (slotCount + 1);
                    await UniTask.Delay(getIntervalMs(), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 4_Result.json을 로드하고, 타이프라이터·패널 자세처럼 다른 컴포넌트가 가진 값도 여기서 밀어넣는다.
        /// </summary>
        private async UniTask LoadSceneSettingsAsync(CancellationToken ct)
        {
            _sceneSettings = await JsonLoader.LoadAsync<ResultSceneSettings>(Constants.SettingsFiles.Result, ct, _logger);
            if (_sceneSettings is null) return;
            if (_sceneSettings.ClampToValid() && _logger != null)
                _logger.ZLogWarning($"[ResultSequence] 4_Result.json에 음수 값이 있어 그 값은 0으로 씁니다.");

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
        /// </summary>
        private void ApplyTopText()
        {
            // 문구가 비어 있는 레벨(미정)은 씬에 입력해둔 텍스트를 그대로 쓴다.
            if (!topText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultSequence] topText가 할당되지 않아 상단 안내 문구를 표시하지 않습니다.");
                return;
            }

            string baseText = _session != null && _session.currentLevel ? _session.currentLevel.resultTopText : null;
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
        /// </summary>
        private void AnimateTopDots(CancellationToken ct)
        {
            // topText 누락은 ApplyTopText에서 이미 경고했다
            if (!topText) return;

            int baseLength = Mathf.Max(0, topText.textInfo.characterCount - Constants.ResultMessages.ResultTopDots.Length);
            CycleDotsAsync(topText, baseLength, Constants.ResultMessages.ResultTopDotSlotLength, Constants.ResultMessages.ResultTopDotMax,
                () => _sceneSettings?.topDotIntervalMs ?? Constants.ResultMessages.ResultTopDotIntervalMs, ct).Forget();
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

            // 빠르게 오르다 끝에서 감속하는 카운터 연출 (순수 수치 보간이므로 DOVirtual).
            // 정수 값이 바뀐 프레임에만 문자열을 만들고 텍스트 메시를 다시 그린다
            int shown = 0;
            await DOVirtual.Float(0f, target, _sceneSettings?.effCountDuration ?? effCountDuration, v =>
                {
                    int value = Mathf.Clamp(Mathf.RoundToInt(v), 0, target);
                    if (value == shown) return;
                    shown = value;
                    text.text = FormatEfficiency(value);
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
