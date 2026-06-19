using UnityEngine;

namespace DG.Data
{
    [CreateAssetMenu(fileName = "GameSession", menuName = "DG/Game Session")]
    public class GameSession : ScriptableObject
    {
        private static GameSession _instance;
        public static GameSession Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<GameSession>("Data/GameSession");
                return _instance;
            }
        }

        public LevelData currentLevel;
    }
}
