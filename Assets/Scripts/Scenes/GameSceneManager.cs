using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Data;
using DG.Game;
using DG.Game.Runtime;
using UnityEngine;

namespace DG.Scenes
{
    public class GameSceneManager : MonoBehaviour
    {
        [SerializeField] private BlockSpawner    blockSpawner;
        [SerializeField] private BlockLayoutData testLayout;
        [SerializeField] private CodingZone      codingZone;

        private CancellationTokenSource _cts;

        private void Start()
        {
            var level  = GameSession.Instance?.currentLevel;
            var layout = level?.blockLayout ?? testLayout;

            if (layout == null)
            {
                Debug.LogWarning("[GameSceneManager] No layout. testLayout을 Inspector에 할당하세요.");
                return;
            }

            blockSpawner.Spawn(layout);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
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
            executor.OnComplete += () => Debug.Log("[실행] 완료");

            await executor.RunAsync(result.Instructions, _cts.Token);
        }
    }
}
