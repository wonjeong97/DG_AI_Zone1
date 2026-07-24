using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game;
using DG.Game.Runtime;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ZLogger;

namespace DG.Scenes
{
    public class GameSceneManager : MonoBehaviour
    {
        [SerializeField] private BlockSpawner    blockSpawner;
        [SerializeField] private LevelData       testLevel;
        [SerializeField] private CodingZone      codingZone;
        [SerializeField] private Button          compileButton;
        [SerializeField] private Button          storyButton;
        [SerializeField] private Button          skipButton;
        [SerializeField] private StoryPanel      storyPanel;
        [SerializeField] private TextMeshProUGUI questionText;

        [Inject] private GameSession _session;
        [Inject] private ILogger<GameSceneManager> _log;

        private CancellationTokenSource _cts;
        private string _questionTime;
        private string _windDirection;
        private string _currentLevelName;

        private void Start()
        {
            LevelData level = _session ? _session.currentLevel : null;
            if (!level) level = testLevel;

            _currentLevelName = level ? level.name : null;

            BlockLayoutData layout = level ? level.blockLayout : null;
            if (!layout)
            {
                _log?.ZLogWarning($"[GameSceneManager] No layout. testLevel을 Inspector에 할당하세요.");
                return;
            }

            // 함수 블록이 포함된 레벨(레벨5)에서는 메인 체인에 함수 블록만 연결하도록 제한
            CodingBlock.RestrictMainChainToFunction = HasFunctionBlock(layout);

            // UniTask는 1회만 await 가능 — Preserve 없이는 Forget()과 SceneFader의 대기가
            // 이중 소비되어 예외로 대기가 무시되고 페이드인이 스폰 완료 전에 시작됨
            UniTask spawnTask = blockSpawner.Spawn(layout).Preserve();
            SceneFader.RegisterPendingTask(spawnTask);
            spawnTask.Forget();

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
                storyButton.onClick.AddListener(() => storyPanel.Show(_session ? _session.unlockedLevelIndex : 0));

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
            _cts = new CancellationTokenSource();

            if (!codingZone)
                codingZone = FindObjectOfType<CodingZone>();

            if (!codingZone)
            {
                _log?.ZLogWarning($"[Compile] CodingZone을 찾을 수 없습니다.");
                return;
            }

            // 이전 에러 하이라이트 초기화 (인벤토리 포함 씬 전체)
            foreach (CodingBlock b in FindObjectsOfType<CodingBlock>())
                b.ClearErrorHighlight();

            var result = BlockCompiler.Compile(codingZone);

            // 컴파일 결과를 코드 형태로 로그 (실패 시에도 순회된 프로그램을 표시)
            LogCompileResult(result);

            if (!result.Success)
            {
                if (result.ErrorBlocks is not null)
                    foreach (CodingBlock b in result.ErrorBlocks)
                        b?.ShowErrorHighlight();

                if (result.Error != null && result.Error.Contains("사용되지 않은"))
                {
                    var categoryZone = FindObjectOfType<CategoryZone>();
                    if (categoryZone && result.ErrorBlocks is not null && result.ErrorBlocks.Length > 0 && result.ErrorBlocks[0] != null)
                    {
                        categoryZone.Select(result.ErrorBlocks[0].Category);
                    }
                }
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
                _log?.ZLogInformation($"[Compile] 컴파일만 수행 — 씬 전환 없음");
                return;
            }

            // 실행~씬 전환 중 연타 방지 (성공 시 씬을 떠나므로 재활성화 불필요)
            if (compileButton) compileButton.interactable = false;

            int score = BlockScorer.ScoreProgram(result.Instructions, _questionTime, _currentLevelName);
            if (_session)
            {
                _session.lastScore = score;
                _session.lastQuestionTime = _questionTime;
                (_session.lastDirection, _session.lastAngle, _session.lastCount) =
                    BlockScorer.ExtractValues(result.Instructions);
            }
            _log?.ZLogInformation($"[점수] {score}점 (기준 시간: {_questionTime})");

            var executor = new BlockExecutor();
            executor.OnBlockEnter = block => { if (block) _log?.ZLogInformation($"[실행] {block.name}"); };
            executor.OnExecute    = async (instr, ct) =>
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
                await UniTask.Delay(200, cancellationToken: ct);
                return true;
            };
            executor.OnCondition = _ => false;
            executor.OnComplete += () =>
            {
                _log?.ZLogInformation($"[실행] 완료 — {score}점");
                SceneFader.FadeAndLoad(Constants.Scenes.Result, logger: _log).Forget();
            };

            await executor.RunAsync(result.Instructions, _cts.Token);
        }

        // 컴파일 결과를 코드 형태(START/…/END)로 로그. 순회 전 실패면 결과 라인만 출력.
        private void LogCompileResult(CompileResult result)
        {
            string prefix = result.Program is not null
                ? $"\n{ProgramFormatter.ToCode(result.Program, result.ReachedEnd)}\n\n결과: "
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
            foreach (var instr in instructions)
            {
                if (instr.Source) instr.Source.ShowSuccessHighlight();
                switch (instr)
                {
                    case RepeatInstruction rep when rep.Body is not null:
                        HighlightSources(rep.Body);
                        break;
                    case IfInstruction ifInstr:
                        if (ifInstr.Then is not null) HighlightSources(ifInstr.Then);
                        if (ifInstr.Else is not null) HighlightSources(ifInstr.Else);
                        break;
                }
            }
        }
    }
}
