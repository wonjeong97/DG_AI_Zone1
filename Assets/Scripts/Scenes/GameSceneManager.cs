using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game;
using DG.Game.Runtime;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private Text            questionText;

        [Inject] private GameSession _session;
        [Inject] private ILogger<GameSceneManager> _log;

        private readonly static string[] QuestionTimes =
            { "아침 8시", "오전 10시", "정오", "오후 2시", "오후 4시" };

        private CancellationTokenSource _cts;
        private string _questionTime;

        private void Start()
        {
            LevelData level = _session ? _session.currentLevel : null;
            if (!level) level = testLevel;

            BlockLayoutData layout = level ? level.blockLayout : null;
            if (!layout)
            {
                _log?.ZLogWarning($"[GameSceneManager] No layout. testLevel을 Inspector에 할당하세요.");
                return;
            }

            // UniTask는 1회만 await 가능 — Preserve 없이는 Forget()과 SceneFader의 대기가
            // 이중 소비되어 예외로 대기가 무시되고 페이드인이 스폰 완료 전에 시작됨
            UniTask spawnTask = blockSpawner.Spawn(layout).Preserve();
            SceneFader.RegisterPendingTask(spawnTask);
            spawnTask.Forget();

            _questionTime = QuestionTimes[Random.Range(0, QuestionTimes.Length)];
            if (questionText)
                questionText.text = $"<color=yellow>현재 {_questionTime}</color>입니다. 태양광 패널이 어느 방향으로\n향해 있어야 할까요? 알맞은 블록을 사용하여 코딩해봅시다.";

            // 코딩 완료 없이 넘어가면 결과 씬에서 '-'로 표시되도록 이전 결과 초기화
            if (_session)
            {
                _session.lastQuestionTime = _questionTime;
                _session.lastDirection = _session.lastAngle = _session.lastCount = null;
                _session.lastScore = 0;
            }

            if (compileButton)
                compileButton.onClick.AddListener(() => CompileAndRun().Forget());

            if (storyButton)
                storyButton.onClick.AddListener(() => storyPanel.Show(_session ? _session.unlockedLevelIndex : 0, level ? level.storyText : null));

            if (skipButton)
                skipButton.onClick.AddListener(SkipToResult);
        }

        private void Update()
        {
            // 컴파일 성공 후에는 스페이스 단축키도 차단
            if (Input.GetKeyDown(KeyCode.Space) && (!compileButton || compileButton.interactable))
                CompileAndRun().Forget();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async UniTaskVoid CompileAndRun()
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
            if (!result.Success)
            {
                _log?.ZLogWarning($"[Compile] 실패: {result.Error}");
                if (result.ErrorBlocks is not null)
                    foreach (CodingBlock b in result.ErrorBlocks)
                        b?.ShowErrorHighlight();

                if (result.Error != null && result.Error.Contains("사용되지 않은 명령 블록이 있습니다"))
                {
                    var categoryZone = FindObjectOfType<CategoryZone>();
                    if (categoryZone)
                    {
                        categoryZone.Select(BlockCategory.Command);
                    }
                }
                return;
            }

            _log?.ZLogInformation($"[Compile] 성공 — {result.Instructions.Count}개 명령");

            // 실행~씬 전환 중 연타 방지 (성공 시 씬을 떠나므로 재활성화 불필요)
            if (compileButton) compileButton.interactable = false;

            // 시작하기 ~ 완성하기 체인 전체에 성공(초록) 외곽선 표시
            foreach (CodingBlock b in codingZone.GetComponentsInChildren<CodingBlock>())
                if (b.Category == BlockCategory.Control)
                    b.ShowSuccessHighlight();
            HighlightSources(result.Instructions);

            int score = BlockScorer.ScoreProgram(result.Instructions, _questionTime);
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
                SceneFader.FadeAndLoad("4_Result", logger: _log).Forget();
            };

            await executor.RunAsync(result.Instructions, _cts.Token);
        }

        // 넘어가기 — 블록 조립 여부와 무관하게 실패로 처리하고 결과 씬으로 이동
        private void SkipToResult()
        {
            if (_session)
            {
                _session.lastScore = 0;
                _session.lastDirection = _session.lastAngle = _session.lastCount = null;
            }
            SceneFader.FadeAndLoad("4_Result", logger: _log).Forget();
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
