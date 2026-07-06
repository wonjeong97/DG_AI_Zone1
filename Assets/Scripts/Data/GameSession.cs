using UnityEngine;

namespace DG.Data
{
    [CreateAssetMenu(fileName = "GameSession", menuName = "DG/Game Session")]
    public class GameSession : ScriptableObject
    {
        public LevelData currentLevel;

        // 마지막 실행 결과 — 결과 씬 표시용 (런타임 전용, 저장 안 함)
        [System.NonSerialized] public int lastScore;
        [System.NonSerialized] public string lastQuestionTime;
        [System.NonSerialized] public string lastDirection;
        [System.NonSerialized] public string lastAngle;
        [System.NonSerialized] public string lastCount;

        private const string UnlockedLevelIndexKey = "UnlockedLevelIndex";

        // 서버 연동 전까지 PlayerPrefs에 로컬로 저장. 추후 서버 값으로 대체될 예정.
        public int unlockedLevelIndex
        {
            get => PlayerPrefs.GetInt(UnlockedLevelIndexKey, 0);
            set
            {
                PlayerPrefs.SetInt(UnlockedLevelIndexKey, value);
                PlayerPrefs.Save();
            }
        }

        // 앱을 껐다 켜면 항상 처음부터 시작하도록 부팅 시점에 진행도 초기화
        public void ResetProgress()
        {
            unlockedLevelIndex = 0;
            currentLevel = null;
            lastScore = 0;
            lastQuestionTime = null;
            lastDirection = null;
            lastAngle = null;
            lastCount = null;
        }
    }
}
