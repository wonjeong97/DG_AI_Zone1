using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using Game;
using Game.Runtime;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
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

        private GameSession _session;
        private ILogger<GameSceneManager> _log;

        [Inject]
        public void Construct(GameSession session, ILogger<GameSceneManager> log)
        {
            _session = session;
            _log = log;
        }

        // 명령 하나를 실행한 것처럼 보이도록 두는 간격
        private const int StepDelayMs = 200;

        private CancellationTokenSource _cts;
        private string _questionTime;
        private string _currentLevelName;
        private LevelData _currentLevel;

        private void Start()
        {
            LevelData level = _session ? _session.currentLevel : null;
            if (!level) level = testLevel;

            _currentLevel = level;
            _currentLevelName = level ? level.name : null;

            BlockLayoutData layout = level ? level.blockLayout : null;
            if (!layout)
            {
                _log?.ZLogWarning($"[GameSceneManager] No layout. testLevel을 Inspector에 할당하세요.");
                return;
            }

            // 함수 블록이 포함된 레벨(레벨5)에서는 메인 체인에 함수 블록만 연결하도록 제한
            CodingBlock.RestrictMainChainToFunction = HasFunctionBlock(layout);

            // 씬 진입 페이드인이 블록 스폰과 카테고리 구성 완료 후에 시작되도록 등록
            SceneFader.RegisterPendingTask(blockSpawner.Spawn(layout));

            // 레벨별 문제 출제 — Constants.Questions 센터에서 생성
            var issue = Constants.Questions.GenerateQuestion(_currentLevelName);
            _questionTime = issue.ValueKey;
            string question = issue.QuestionText;

            if (questionText)
                questionText.text = question;
            else
                _log?.ZLogWarning($"[GameSceneManager] questionText가 할당되지 않아 문제 텍스트를 표시할 수 없습니다.");

            // 코딩 완료 없이 넘어가면 결과 씬에서 '-'로 표시되도록 이전 결과 초기화
            if (_session)
            {
                _session.lastQuestionTime = _questionTime;
                _session.lastDirection = _session.lastAngle = _session.lastCount = null;
                _session.lastScore = 0;
            }

            if (compileButton)
                compileButton.onClick.AddListener(() => CompileAndRun(advanceScene: true).Forget());

            if (storyButton)
                storyButton.onClick.AddListener(() => storyPanel.Show(
                    _currentLevelName,
                    _currentLevel ? _currentLevel.storyText : null));

            if (hintButton)
                hintButton.onClick.AddListener(() => hintPanel.Show(_currentLevelName, _questionTime));

            if (skipButton)
                skipButton.onClick.AddListener(SkipToResult);
        }

        private void Update()
        {
            // 스페이스바: 컴파일 검증만 (채점·실행·씬 전환 없음)
            if (Input.GetKeyDown(KeyCode.Space) && (!compileButton || compileButton.interactable))
                CompileAndRun(advanceScene: false).Forget();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async UniTaskVoid CompileAndRun(bool advanceScene)
        {
            // 진행 중인 실행 중단
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            if (!codingZone)
                codingZone = FindObjectOfType<CodingZone>();

            if (!codingZone)
            {
                _log?.ZLogWarning($"[GameSceneManager] CodingZone을 찾을 수 없습니다.");
                return;
            }

            // 이전 에러 하이라이트 초기화 (인벤토리 포함 씬 전체)
            foreach (CodingBlock b in FindObjectsOfType<CodingBlock>())
                b.ClearErrorHighlight();

            var result = BlockCompiler.Compile(codingZone);

            // 컴파일 성공 시에만 점수를 계산 — 포매터/로그에 함께 표시
            int? score = result.Success
                ? BlockScorer.ScoreProgram(result.Instructions, _questionTime, _currentLevelName)
                : null;

            // 컴파일 결과를 코드 형태로 로그 (실패 시에도 순회된 프로그램을 표시)
            LogCompileResult(result, score);

            if (!result.Success)
            {
                ShowCompileError(result);
                return;
            }

            // 시작하기 ~ 완성하기 체인 전체에 성공(초록) 외곽선 표시
            foreach (CodingBlock b in codingZone.GetComponentsInChildren<CodingBlock>())
                if (b.Category == BlockCategory.Control)
                    b.ShowSuccessHighlight();
            HighlightSources(result.Instructions);

            // 스페이스바: 컴파일 검증까지만 — 채점·실행·씬 전환은 완료 버튼 전용
            if (!advanceScene)
            {
                _log?.ZLogInformation($"[GameSceneManager] 컴파일만 수행 — 씬 전환 없음");
                return;
            }

            // 실행~씬 전환 중 연타 방지 (성공 시 씬을 떠나므로 재활성화 불필요)
            if (compileButton) compileButton.interactable = false;

            if (_session)
            {
                _session.lastScore = score.Value;
                _session.lastQuestionTime = _questionTime;
                (_session.lastDirection, _session.lastAngle, _session.lastCount) =
                    BlockScorer.ExtractValues(result.Instructions);
            }
            _log?.ZLogInformation($"[GameSceneManager] 점수: {score}점 (기준 시간: {_questionTime})");

            BlockExecutor executor = CreateExecutor(score.Value);
            await executor.RunAsync(result.Instructions, _cts.Token);
        }

        // 컴파일 실패 표시 — 문제 블록에 에러 외곽선을 켜고,
        // '사용되지 않은 블록' 오류는 해당 블록이 보이도록 인벤토리 탭까지 전환한다.
        private static void ShowCompileError(CompileResult result)
        {
            if (result.ErrorBlocks is null) return;

            foreach (CodingBlock b in result.ErrorBlocks)
                b?.ShowErrorHighlight();

            if (result.ErrorKind != CompileErrorKind.UnusedBlocks) return;
            if (result.ErrorBlocks.Length == 0 || !result.ErrorBlocks[0]) return;

            CategoryZone categoryZone = FindObjectOfType<CategoryZone>();
            if (categoryZone) categoryZone.Select(result.ErrorBlocks[0].Category);
        }

        // 현재는 실행 자체가 로그 재생 연출 — 각 명령을 로그로 남기고 일정 간격으로 진행한 뒤 결과 씬으로 넘어간다.
        private BlockExecutor CreateExecutor(int score)
        {
            var executor = new BlockExecutor();

            executor.OnBlockEnter = block => { if (block) _log?.ZLogInformation($"[GameSceneManager] 블록 실행: {block.name}"); };
            executor.OnExecute = async (instr, ct) =>
            {
                switch (instr)
                {
                    case CommandInstruction cmd:
                        _log?.ZLogInformation($"  Command: {cmd.Command}  Value: {cmd.Value ?? "(없음)"}");
                        break;
                    case ActionInstruction act:
                        _log?.ZLogInformation($"  Action: {act.Action}");
                        break;
                    case ConditionActionInstruction cond:
                        _log?.ZLogInformation($"  ConditionAction: {cond.Action}");
                        break;
                }
                await UniTask.Delay(StepDelayMs, cancellationToken: ct);
                return true;
            };

            // 조건 평가기는 아직 미구현 — 항상 else 분기를 탄다
            executor.OnCondition = _ => false;
            executor.OnComplete += () =>
            {
                _log?.ZLogInformation($"[GameSceneManager] 실행 완료 — {score}점");
                SceneFader.FadeAndLoad(Constants.Scenes.Result, logger: _log).Forget();
            };

            return executor;
        }

        // 컴파일 결과를 코드 형태(START/…/END)로 로그. 순회 전 실패면 결과 라인만 출력.
        private void LogCompileResult(CompileResult result, int? score)
        {
            string prefix = result.Program is not null
                ? $"\n{ProgramFormatter.ToCode(result.Program, result.ReachedEnd, score)}\n\n결과: "
                : "결과: ";

            if (result.Success)
                _log?.ZLogInformation($"{prefix}컴파일 성공");
            else
                _log?.ZLogWarning($"{prefix}컴파일 실패 - {result.Error}");
        }

        // 레이아웃 인벤토리에 함수/함수 정의 블록이 있는지 (레벨5 판별)
        private static bool HasFunctionBlock(BlockLayoutData layout)
        {
            if (layout.inventoryBlocks is not null)
                foreach (BlockEntry e in layout.inventoryBlocks)
                    if (e.category == BlockCategory.Function || e.category == BlockCategory.FunctionDef)
                        return true;
            return false;
        }

        // 넘어가기 — 블록 조립 여부와 무관하게 실패로 처리하고 결과 씬으로 이동
        private void SkipToResult()
        {
            if (_session)
            {
                _session.lastScore = 0;
                _session.lastDirection = _session.lastAngle = _session.lastCount = null;
            }
            SceneFader.FadeAndLoad(Constants.Scenes.Result, logger: _log).Forget();
        }

        // 프로그램에 포함된 모든 블록(반복/조건 내부 포함)에 성공 외곽선 표시
        private static void HighlightSources(List<BlockInstruction> instructions)
        {
            foreach (BlockInstruction instr in instructions)
            {
                if (instr.Source) instr.Source.ShowSuccessHighlight();

                // Value 블록은 인스트럭션 트리에 별도 노드로 나타나지 않으므로 따로 표시
                if (instr is CommandInstruction cmd && cmd.ValueSource)
                    cmd.ValueSource.ShowSuccessHighlight();

                // 반복하기 헤더의 횟수 Value 블록도 마찬가지
                if (instr is RepeatInstruction rep && rep.ValueSource)
                    rep.ValueSource.ShowSuccessHighlight();

                if (instr is IfInstruction ifInstr)
                {
                    // '아니면' 마커는 Then/Else 어느 리스트에도 포함되지 않으므로 따로 표시
                    if (ifInstr.ElseMarkerSource)
                        ifInstr.ElseMarkerSource.ShowSuccessHighlight();

                    // 조건 블록(들)도 인스트럭션 트리에 별도 노드로 나타나지 않으므로 따로 표시
                    HighlightCondition(ifInstr.Condition);
                }

                // 함수 본문은 제외 — 함수 정의는 메인 체인 밖의 별도 컨테이너라 대상이 아니다
                if (instr is FunctionInstruction) continue;

                foreach (List<BlockInstruction> body in InstructionTree.ChildBodies(instr))
                    HighlightSources(body);
            }
        }

        // 만약 헤더에 연결된 조건 블록(들)에 성공 외곽선 표시 — 그리고/또는(Logic)이면 좌우 조건까지
        private static void HighlightCondition(ConditionExpr condition)
        {
            switch (condition)
            {
                case SimpleConditionExpr simple:
                    if (simple.Source) simple.Source.ShowSuccessHighlight();
                    break;
                case LogicConditionExpr logic:
                    if (logic.Source) logic.Source.ShowSuccessHighlight();
                    if (logic.Left?.Source) logic.Left.Source.ShowSuccessHighlight();
                    if (logic.Right?.Source) logic.Right.Source.ShowSuccessHighlight();
                    break;
            }
        }
    }
}
