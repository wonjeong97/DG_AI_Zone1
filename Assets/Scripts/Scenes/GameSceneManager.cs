using DG.Data;
using DG.Game;
using UnityEngine;

namespace DG.Scenes
{
    public class GameSceneManager : MonoBehaviour
    {
        [SerializeField] private BlockSpawner blockSpawner;
        [SerializeField] private BlockLayoutData testLayout;  // 에디터 직접 테스트용

        private void Start()
        {
            var level = GameSession.Instance?.currentLevel;
            var layout = level?.blockLayout ?? testLayout;

            if (layout == null)
            {
                Debug.LogWarning("[GameSceneManager] No layout. testLayout을 Inspector에 할당하세요.");
                return;
            }

            blockSpawner.Spawn(layout);
        }
    }
}
