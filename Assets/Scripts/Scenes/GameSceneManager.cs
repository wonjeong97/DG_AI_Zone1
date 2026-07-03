using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game;
using DG.Game.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        private readonly static string[] QuestionTimes =
            { "아침 8시", "오전 10시", "정오", "오후 2시", "오후 4시" };

        private CancellationTokenSource _cts;
        private string _questionTime;

        private void Start()
        {
            LevelData level = GameSession.Instance ? GameSession.Instance.currentLevel : null;
            if (!level) level = testLevel;

            BlockLayoutData layout = level ? level.blockLayout : null;
            if (layout == null)
            {
                Debug.LogWarning("[GameSceneManager] No layout. testLevel을 Inspector에 할당하세요.");
                return;
            }

            blockSpawner.Spawn(layout);

            _questionTime = QuestionTimes[Random.Range(0, QuestionTimes.Length)];
            if (questionText)
                questionText.text = $"<color=yellow>현재 {_questionTime}</color>입니다. 태양광 패널이 어느 방향으로\n향해 있어야 할까요? 알맞은 블록을 사용하여 코딩해봅시다.";

            // 코딩 완료 없이 넘어가면 결과 씬에서 '-'로 표시되도록 이전 결과 초기화
            GameSession startSession = GameSession.Instance;
            if (startSession)
            {
                startSession.lastQuestionTime = _questionTime;
                startSession.lastDirection = startSession.lastAngle = startSession.lastCount = null;
                startSession.lastScore = 0;
            }

            if (compileButton)
                compileButton.onClick.AddListener(() => CompileAndRun().Forget());

            if (storyButton)
                storyButton.onClick.AddListener(() => storyPanel.Show(GameSession.Instance?.unlockedLevelIndex ?? 0, level ? level.storyText : null));

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

            if (codingZone == null)
                codingZone = FindObjectOfType<CodingZone>();

            if (codingZone == null)
            {
                Debug.LogWarning("[Compile] CodingZone을 찾을 수 없습니다.");
                return;
            }

            // 이전 에러 하이라이트 초기화 (인벤토리 포함 씬 전체)
            foreach (var b in FindObjectsOfType<CodingBlock>())
                b.ClearErrorHighlight();

            var result = BlockCompiler.Compile(codingZone);
            if (!result.Success)
            {
                Debug.LogWarning($"[Compile] 실패: {result.Error}");
                if (result.ErrorBlocks != null)
                    foreach (var b in result.ErrorBlocks)
                        b?.ShowErrorHighlight();
                return;
            }

            Debug.Log($"[Compile] 성공 — {result.Instructions.Count}개 명령");

            // 실행~씬 전환 중 연타 방지 (성공 시 씬을 떠나므로 재활성화 불필요)
            if (compileButton) compileButton.interactable = false;

            // 시작하기 ~ 종료하기 체인 전체에 성공(초록) 외곽선 표시
            foreach (CodingBlock b in codingZone.GetComponentsInChildren<CodingBlock>())
                if (b.Category == BlockCategory.Control)
                    b.ShowSuccessHighlight();
            HighlightSources(result.Instructions);

            int score = BlockScorer.ScoreProgram(result.Instructions, _questionTime);
            GameSession session = GameSession.Instance;
            if (session)
            {
                session.lastScore = score;
                session.lastQuestionTime = _questionTime;
                (session.lastDirection, session.lastAngle, session.lastCount) =
                    BlockScorer.ExtractValues(result.Instructions);
            }
            Debug.Log($"[점수] {score}점 (기준 시간: {_questionTime})");

            var executor = new BlockExecutor();
            executor.OnBlockEnter = block => { if (block) Debug.Log($"[실행] {block.name}"); };
            executor.OnExecute    = async (instr, ct) =>
            {
                switch (instr)
                {
                    case CommandInstruction cmd:
                        Debug.Log($"  Command: {cmd.Command}  Value: {cmd.Value ?? "(없음)"}");
                        break;
                    case ActionInstruction act:
                        Debug.Log($"  Action: {act.Action}");
                        break;
                    case ConditionActionInstruction cond:
                        Debug.Log($"  ConditionAction: {cond.Action}");
                        break;
                }
                await UniTask.Delay(200, cancellationToken: ct);
                return true;
            };
            executor.OnCondition = _ => false;
            executor.OnComplete += () =>
            {
                Debug.Log($"[실행] 완료 — {score}점");
                SceneManager.LoadScene("5_Result");
            };

            await executor.RunAsync(result.Instructions, _cts.Token);
        }

        // 넘어가기 — 블록 조립 여부와 무관하게 실패로 처리하고 결과 씬으로 이동
        private static void SkipToResult()
        {
            GameSession session = GameSession.Instance;
            if (session)
            {
                session.lastScore = 0;
                session.lastDirection = session.lastAngle = session.lastCount = null;
            }
            SceneManager.LoadScene("5_Result");
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
