using System;
using System.Collections.Generic;
using System.Threading;
using App;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using Game;
using Game.Runtime;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using VContainer;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace Scenes
{
    public class GameSceneManager : MonoBehaviour
    {
        [SerializeField] private BlockSpawner    blockSpawner;
        [SerializeField] private LevelData       testLevel;
        [SerializeField] private CodingZone      codingZone;
        [SerializeField] private Button          compileButton;
        [SerializeField] private Button          storyButton;
        [SerializeField] private Button          hintButton;
        [SerializeField] private Button          skipButton;
        [SerializeField] private StoryPanel      storyPanel;
        [SerializeField] private HintPanel       hintPanel;
        [SerializeField] private TextMeshProUGUI questionText;

        [Tooltip("컴파일 성공/에러를 블록에 표시하는 방식 — 외곽선 또는 블록 색상 틴트")]
        [SerializeField] private HighlightMode highlightMode = HighlightMode.Outline;

        private GameSession _session;
        private ILogger<GameSceneManager> _logger;
        private SoundManager _soundManager;
        private VisitorInfoProvider _visitorInfoProvider; // 행동 로그 주어

        /// <summary>
        /// 게임 세션, 로거, 사운드 매니저, 체험자 정보를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(GameSession session, ILogger<GameSceneManager> logger, SoundManager soundManager,
            VisitorInfoProvider visitorInfoProvider)
        {
            _session = session;
            _logger = logger;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
        }

        // 명령 하나를 실행한 것처럼 보이도록 두는 간격 — 3_Game.json 로드 전까지의 폴백 기본값
        private const int StepDelayMs = 200;

        // 3_Game.json 튜닝 값 — 씬 페이드인 전에 로드된다
        private GameSceneSettings _sceneSettings;

        private CancellationTokenSource _cts;
        private GameInputActions _input; // 디버그 단축키(Space) — 컴파일 검증
        private string _questionTime;
        private string _correctAnswer; // 방향 문제의 정답 블록 라벨 — 정답 방향이 없는 레벨은 null
        private LevelData _currentLevel;

        // 레벨 없이 진입하면(testLevel 미지정) 태양광 규칙으로 동작한다
        private LevelKind CurrentLevelKind => _currentLevel ? _currentLevel.kind : LevelKind.Solar;

        // 행동 로그에 쓰는 레벨 번호(1부터) — 레벨 데이터가 없으면 0
        private int LevelNumber => _currentLevel ? _currentLevel.levelIndex + 1 : 0;

        /// <summary>
        /// 레벨을 결정해 블록을 스폰하고 문제를 출제한 뒤 버튼 동작을 연결한다.
        /// </summary>
        private void Start()
        {
            if (_logger == null)
                Debug.LogError("[GameSceneManager] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");

            // UI 작업 중 에디터에서 켜둔 채로 남아있어도, 씬 시작 시 팝업 패널(스토리/힌트)은 항상 닫힌 상태로 시작
            if (storyPanel) storyPanel.gameObject.SetActive(false);
            if (hintPanel) hintPanel.gameObject.SetActive(false);

            // 레이아웃이 없어도 넘어가기로 빠져나갈 수 있도록 버튼부터 연결한다
            BindButtons();

            CodingBlock.Mode = highlightMode;

            // 연출·감도 튜닝 값은 블록이 뜨기 전에 준비되어야 하므로 씬 페이드인 대기 작업으로 등록
            SceneFader.RegisterPendingTask(LoadSceneSettingsAsync());

            LevelData level = _session != null ? _session.currentLevel : null;

            // 2_Story를 거치지 않고 3_Game에서 바로 Play한 경우 — testLevel로 대체하고 세션에도 반영한다.
            // 결과 씬은 _session.currentLevel만 보기 때문에, 반영하지 않으면 4_Result가 레벨을 모른 채
            // 레벨1 기준으로 동작한다. 타이틀로 돌아가면 ResetProgress가 currentLevel을 비우므로 값이 남지도 않는다.
            if (!level)
            {
                level = testLevel;
                if (_session != null) _session.currentLevel = level;
            }

            _currentLevel = level;

            BlockLayoutData layout = level ? level.blockLayout : null;
            if (!layout)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameSceneManager] No layout. testLevel을 Inspector에 할당하세요.");
                return;
            }

            // 씬 진입 페이드인이 블록 스폰과 카테고리 구성 완료 후에 시작되도록 등록
            if (blockSpawner)
                SceneFader.RegisterPendingTask(blockSpawner.Spawn(layout, destroyCancellationToken));
            else if (_logger != null)
                _logger.ZLogWarning($"[GameSceneManager] blockSpawner가 할당되지 않아 블록을 생성할 수 없습니다.");

            // 레벨별 문제 출제 — LevelData의 문제 값 후보 중 하나를 무작위로 고른다
            QuestionOption option = level.PickQuestionOption();
            _questionTime = option is not null ? option.value : null;
            _correctAnswer = level.GetCorrectAnswer(_questionTime);

            string question = null;
            if (option is not null && !string.IsNullOrEmpty(level.questionFormat))
                question = ZString.Format(level.questionFormat, option.value);
            else if (_logger != null)
                _logger.ZLogWarning($"[GameSceneManager] {level.name}에 문제 문구나 문제 값 후보가 없어 문제를 출제하지 못했습니다.");

            // 문구 길이는 레벨마다 달라도 문제 칸(Text_Question)의 Auto Size가 글자 크기를 맞춘다
            if (questionText)
                questionText.text = question;
            else if (_logger != null)
                _logger.ZLogWarning($"[GameSceneManager] questionText가 할당되지 않아 문제 텍스트를 표시할 수 없습니다.");

            // 코딩 완료 없이 넘어가면 결과 씬에서 '-'로 표시되도록 이전 결과 초기화
            if (_session != null)
            {
                _session.ResetLastResult();
                _session.lastQuestionTime = _questionTime;
            }
        }

        /// <summary>
        /// 코딩 완료·미션 다시보기·힌트·넘어가기 버튼에 동작을 연결한다.
        /// </summary>
        private void BindButtons()
        {
            // 코딩 완료 버튼은 클릭음 대신 컴파일 결과에 따라 완료음·경고음을 낸다(CompileAndRun)
            if (compileButton)
                compileButton.onClick.AddListener(() => StartCompileAndRun(advanceScene: true));
            else if (_logger != null)
                _logger.ZLogError($"[GameSceneManager] compileButton이 할당되지 않아 코딩을 완료할 수 없습니다.");

            if (storyButton && storyPanel)
                storyButton.onClick.AddListener(() =>
                {
                    if (_logger != null) _logger.ZLogInformation($"[GameSceneManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 미션 다시보기를 누름.");
                    if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.HintEpisode);
                    storyPanel.Show(_currentLevel);
                });
            else if (_logger != null)
                _logger.ZLogWarning($"[GameSceneManager] storyButton 또는 storyPanel이 할당되지 않아 미션 다시보기를 쓸 수 없습니다.");

            if (hintButton && hintPanel)
                hintButton.onClick.AddListener(() =>
                {
                    if (_logger != null) _logger.ZLogInformation($"[GameSceneManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 힌트를 누름.");
                    if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.HintEpisode);
                    hintPanel.Show(_currentLevel, _questionTime);
                });
            else if (_logger != null)
                _logger.ZLogWarning($"[GameSceneManager] hintButton 또는 hintPanel이 할당되지 않아 힌트를 쓸 수 없습니다.");

            if (skipButton)
                skipButton.onClick.AddListener(SkipToResult);
            else if (_logger != null)
                _logger.ZLogWarning($"[GameSceneManager] skipButton이 할당되지 않아 미션을 건너뛸 수 없습니다.");
        }

        /// <summary>
        /// 코딩 완료·미션 다시보기·힌트·넘어가기 버튼을 함께 켜거나 끈다 — 성공 연출부터 결과 씬 전환까지는 모두 막는다.
        /// </summary>
        private void SetPlayButtonsInteractable(bool interactable)
        {
            if (compileButton) compileButton.interactable = interactable;
            if (storyButton) storyButton.interactable = interactable;
            if (hintButton) hintButton.interactable = interactable;
            if (skipButton) skipButton.interactable = interactable;
        }

        /// <summary>
        /// 디버그 단축키(Space) 입력을 받기 시작한다 — 현장 빌드에서 관람객이 누르지 않도록 에디터·개발 빌드에서만.
        /// </summary>
        private void OnEnable()
        {
            if (!Debug.isDebugBuild) return;

            _input ??= new GameInputActions();
            _input.Debug.Shortcut.performed += OnDebugShortcut;
            _input.Debug.Enable();
        }

        /// <summary>
        /// 디버그 단축키 입력을 멈춘다.
        /// </summary>
        private void OnDisable()
        {
            if (_input == null) return;
            _input.Debug.Shortcut.performed -= OnDebugShortcut;
            _input.Debug.Disable();
        }

        /// <summary>
        /// 디버그 단축키(Space) — 컴파일 검증만 수행한다 (채점·실행·씬 전환 없음).
        /// </summary>
        private void OnDebugShortcut(InputAction.CallbackContext _)
        {
            if (!compileButton || compileButton.interactable)
                StartCompileAndRun(advanceScene: false);
        }

        /// <summary>
        /// 진행 중인 컴파일·실행을 취소하고 입력 액션을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            CancelRun();
            _input?.Dispose();
        }

        /// <summary>
        /// 진행 중인 실행을 중단하고 새 CTS로 컴파일·실행을 시작한다.
        /// </summary>
        private void StartCompileAndRun(bool advanceScene)
        {
            CancelRun();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            CompileAndRun(advanceScene, _cts).Forget(); // 토큰이 아니라 CTS 객체를 넘긴다
        }

        /// <summary>
        /// 현재 실행 CTS를 취소·해제한다.
        /// </summary>
        private void CancelRun()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        /// <summary>
        /// 블록을 컴파일해 결과를 표시하고, 성공하면 파도타기 연출 후 채점·실행·결과 씬 전환까지 진행한다.
        /// </summary>
        private async UniTaskVoid CompileAndRun(bool advanceScene, CancellationTokenSource cts)
        {
            // 이후 새 실행이 _cts를 교체·폐기해도 이 흐름은 자기 토큰만 쓴다
            CancellationToken ct = cts.Token;

            try
            {
                if (!codingZone)
                {
                    if (_logger != null) _logger.ZLogWarning($"[GameSceneManager] codingZone이 인스펙터에 연결되지 않았습니다.");
                    return;
                }

                // 이전 에러 하이라이트 초기화 (코딩 패널 + 인벤토리)
                CodingBlock.ClearAllErrorHighlights(codingZone);

                CompileResult result = BlockCompiler.Compile(codingZone);

                // 컴파일 성공 시에만 점수를 계산 — 포매터/로그에 함께 표시
                int? score = result.Success
                    ? BlockScorer.ScoreProgram(result.Instructions, _questionTime, _correctAnswer, CurrentLevelKind, _logger)
                    : null;

                // 코딩 완료 결과를 행동 로그로 남기고 순회한 프로그램을 코드 형태로 덧붙인다 (실패 시에도 표시)
                LogCompileResult(result, score, advanceScene);

                if (!result.Success)
                {
                    if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.CodingAlert);
                    ShowCompileError(result, blockSpawner ? blockSpawner.CategoryZone : null);
                    return;
                }

                // 실행~씬 전환 중 연타·넘어가기 방지 — 파도타기 연출 시작 전에 모두 비활성화 (성공 시 씬을 떠나므로 재활성화 불필요).
                // 넘어가기가 살아 있으면 이미 저장한 성공 결과를 ResetLastResult가 지워 실패로 올라간다
                if (advanceScene) SetPlayButtonsInteractable(false);

                if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.CodingComplete);

                // 시작하기 ~ 완성하기 순서로 성공(초록) 하이라이트가 파도타기처럼 순서대로 켜짐 (값 블록 포함)
                await PlaySuccessWaveAsync(BuildSuccessOrder(codingZone, result.Instructions), ct);

                // 스페이스바: 컴파일 검증까지만 — 채점·실행·씬 전환은 완료 버튼 전용
                if (!advanceScene) return;

                SaveResultToSession(result.Instructions, score.Value);

                BlockExecutor executor = CreateExecutor();
                await executor.RunAsync(result.Instructions, ct);
            }
            catch (OperationCanceledException)
            {
                // 새 컴파일 요청이나 씬 종료로 취소된 정상 흐름 — 별도 처리 불필요
            }
            catch (Exception ex)
            {
                // 예상하지 못한 오류로 결과 씬에 가지 못했으므로 다시 시도하거나 넘어갈 수 있게 버튼을 되살린다
                if (_logger != null) _logger.ZLogError($"[GameSceneManager] 코딩 완료 처리 중 오류가 나 버튼을 다시 켭니다: {ex}");
                if (advanceScene) SetPlayButtonsInteractable(true);
            }
            finally
            {
                // 필드가 아직 자기 CTS일 때만 정리한다 — 새 실행이 이미 교체했다면 그쪽 CTS를 폐기하면 안 됨
                if (_cts == cts)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }

        /// <summary>
        /// 결과 씬 표시에 쓸 점수와 조립 값들을 게임 세션에 기록한다.
        /// </summary>
        private void SaveResultToSession(List<BlockInstruction> instructions, int score)
        {
            if (_session == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameSceneManager] GameSession이 주입되지 않아 결과를 저장하지 못했습니다.");
                return;
            }

            _session.lastScore = score;
            _session.lastQuestionTime = _questionTime;
            (_session.lastDirection, _, _session.lastCount) = BlockScorer.ExtractValues(instructions);
            _session.lastRepeatUsed = BlockScorer.ContainsRepeat(instructions);
            _session.lastGateHeight = BlockScorer.GetHydroGateHeight(instructions);
            _session.lastElseUsed = BlockScorer.HasHydroElse(instructions);
            _session.lastGateOrderCorrect = BlockScorer.IsHydroGateOrderCorrect(instructions);
            _session.lastConditionText = BlockScorer.GetConditionText(instructions);
            _session.lastAmusementPowerCut = BlockScorer.IsAmusementPowerCut(instructions);
            _session.lastHospitalPowerKept = BlockScorer.IsHospitalPowerKept(instructions);
            _session.lastFunctionUsed = BlockScorer.UsesFunction(instructions);
            _session.lastEnergiesInFunction = BlockScorer.GetEnergiesInFunction(instructions);
            _session.hasCodingResult = true;
        }

        /// <summary>
        /// 컴파일 실패를 표시한다 — 문제 블록에 에러 외곽선을 켜고,
        /// '사용되지 않은 블록' 오류는 해당 블록이 보이도록 인벤토리 탭까지 전환한다.
        /// </summary>
        private static void ShowCompileError(CompileResult result, CategoryZone categoryZone)
        {
            if (result.ErrorBlocks is null) return;

            foreach (CodingBlock b in result.ErrorBlocks)
                if (b) b.ShowErrorHighlight();

            if (result.ErrorKind != CompileErrorKind.UnusedBlocks) return;
            if (result.ErrorBlocks.Length == 0 || !result.ErrorBlocks[0]) return;

            if (categoryZone) categoryZone.Select(result.ErrorBlocks[0].Category);
        }

        /// <summary>
        /// 실행기를 만든다 — 현재는 실행 자체가 연출로, 명령마다 일정 간격을 두고 진행한 뒤 결과 씬으로 넘어간다.
        /// </summary>
        private BlockExecutor CreateExecutor()
        {
            BlockExecutor executor = new BlockExecutor();

            executor.OnExecute = async (_, ct) =>
            {
                await UniTask.Delay(_sceneSettings?.executeStepDelayMs ?? StepDelayMs, cancellationToken: ct);
                return true;
            };

            // 조건 평가기는 아직 미구현 — 항상 else 분기를 탄다
            executor.OnCondition = _ => false;
            executor.OnComplete += () => SceneFader.FadeAndLoad(Constants.Scenes.Result, logger: _logger).Forget();

            return executor;
        }

        /// <summary>
        /// 코딩 완료(디버그 단축키면 검증) 결과를 행동 로그 한 줄로 남기고, 순회한 프로그램을 코드 형태(START/…/END)로 덧붙인다.
        /// 순회 전에 실패했으면 결과 줄만 남긴다.
        /// </summary>
        private void LogCompileResult(CompileResult result, int? score, bool advanceScene)
        {
            if (_logger == null) return;

            string action = advanceScene
                ? ZString.Concat(VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider), " 코딩 완료를 누름")
                : "디버그 단축키로 코딩을 검증함";
            string outcome = result.Success
                ? ZString.Concat("성공, ", score ?? 0, "점(문제 값 ", _questionTime ?? "없음", ")")
                : ZString.Concat("실패: ", result.Error);
            string code = result.Program is not null
                ? ZString.Concat("\n", ProgramFormatter.ToCode(result.Program, result.ReachedEnd))
                : string.Empty;

            _logger.ZLogInformation($"[GameSceneManager] {action} — {LevelNumber}레벨 {outcome}.{code}");
        }

        /// <summary>
        /// 블록 조립 여부와 무관하게 실패로 처리하고 결과 씬으로 넘어간다.
        /// </summary>
        private void SkipToResult()
        {
            // 디버그 컴파일 검증이 돌고 있어도 결과를 덮어쓰지 않도록 멈춘다
            CancelRun();

            if (_logger != null) _logger.ZLogInformation($"[GameSceneManager] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 건너뛰기를 누름 — {LevelNumber}레벨 실패로 처리함.");
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            if (_session != null)
            {
                _session.ResetLastResult();
            }
            SceneFader.FadeAndLoad(Constants.Scenes.Result, logger: _logger).Forget();
        }

        /// <summary>
        /// 파도타기 순서를 만든다 — 시작하기 → 프로그램 순서대로(값 블록 포함) → 완성하기.
        /// </summary>
        private static List<CodingBlock> BuildSuccessOrder(CodingZone zone, List<BlockInstruction> instructions)
        {
            List<CodingBlock> order = new List<CodingBlock>();

            CodingBlock startBlock = null, endBlock = null;
            foreach (CodingBlock b in zone.Blocks)
            {
                if (!b || !b.gameObject.activeInHierarchy || !zone.Contains(b)) continue;
                if (b.Category != BlockCategory.Control) continue;
                if (b.ControlRole == Data.ControlRole.Start) startBlock = b;
                else if (b.ControlRole == Data.ControlRole.End) endBlock = b;
            }

            if (startBlock) order.Add(startBlock);
            CollectSuccessOrder(instructions, order);
            if (endBlock) order.Add(endBlock);
            return order;
        }

        /// <summary>
        /// 프로그램에 포함된 모든 블록(반복/조건 내부 포함)을 실행 순서대로 수집한다.
        /// </summary>
        private static void CollectSuccessOrder(List<BlockInstruction> instructions, List<CodingBlock> order)
        {
            foreach (BlockInstruction instr in instructions)
            {
                if (instr.Source) order.Add(instr.Source);

                // Value 블록은 인스트럭션 트리에 별도 노드로 나타나지 않으므로 소유 블록 바로 뒤에 추가
                if (instr is CommandInstruction cmd && cmd.ValueSource)
                    order.Add(cmd.ValueSource);

                // 반복하기 헤더의 횟수 Value 블록도 마찬가지
                if (instr is RepeatInstruction rep && rep.ValueSource)
                    order.Add(rep.ValueSource);

                if (instr is IfInstruction ifInstr)
                {
                    // 조건 블록(들)도 인스트럭션 트리에 별도 노드로 나타나지 않으므로 따로 추가
                    CollectConditionOrder(ifInstr.Condition, order);

                    // '아니면' 마커는 Then/Else 어느 리스트에도 포함되지 않으므로 따로 추가
                    if (ifInstr.ElseMarkerSource)
                        order.Add(ifInstr.ElseMarkerSource);
                }

                // 함수 본문은 제외 — 함수 정의는 메인 체인 밖의 별도 컨테이너라 대상이 아니다
                if (instr is FunctionInstruction) continue;

                foreach (List<BlockInstruction> body in InstructionTree.ChildBodies(instr))
                    CollectSuccessOrder(body, order);
            }
        }

        /// <summary>
        /// 만약 헤더에 연결된 조건 블록(들)을 수집한다 — 그리고/또는(Logic)이면 좌우 조건까지.
        /// </summary>
        private static void CollectConditionOrder(ConditionExpr condition, List<CodingBlock> order)
        {
            switch (condition)
            {
                case SimpleConditionExpr simple:
                    if (simple.Source) order.Add(simple.Source);
                    break;
                case LogicConditionExpr logic:
                    if (logic.Left != null && logic.Left.Source) order.Add(logic.Left.Source);
                    if (logic.Source) order.Add(logic.Source);
                    if (logic.Right != null && logic.Right.Source) order.Add(logic.Right.Source);
                    break;
            }
        }

        /// <summary>
        /// 순서대로 시차를 두고 성공 하이라이트를 켜 파도타기 연출을 만든다.
        /// </summary>
        private async UniTask PlaySuccessWaveAsync(List<CodingBlock> order, CancellationToken ct)
        {
            int stepMs = _sceneSettings?.successWaveStepMs ?? Constants.HighlightSettings.SuccessWaveStepMs;
            foreach (CodingBlock b in order)
            {
                if (!b) continue;
                b.ShowSuccessHighlight();
                await UniTask.Delay(stepMs, cancellationToken: ct);
            }
        }

        /// <summary>
        /// 3_Game.json을 로드한다 — 블록 스냅 감도는 CodingBlock이 정적으로 참조하므로 함께 넘긴다.
        /// </summary>
        private async UniTask LoadSceneSettingsAsync()
        {
            _sceneSettings = await JsonLoader.LoadAsync<GameSceneSettings>(Constants.SettingsFiles.Game, destroyCancellationToken, _logger);
            CodingBlock.Settings = _sceneSettings;
        }
    }
}
