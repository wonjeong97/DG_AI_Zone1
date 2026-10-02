using UnityEngine;

namespace Data
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
        [System.NonSerialized] public bool lastRepeatUsed;   // 레벨2 결과의 '반복 감지' 표시용
        [System.NonSerialized] public string lastGateHeight; // 레벨3 결과의 '수문 개방 높이' — null이면 조건 감지 OFF
        [System.NonSerialized] public bool lastElseUsed;     // 레벨3 결과의 '수문 닫기 조건(아니면)' 표시용
        [System.NonSerialized] public bool lastGateOrderCorrect; // 레벨3 결과의 '수문 열기·닫기 순서' 표시용
        [System.NonSerialized] public string lastConditionText;  // 레벨4 결과의 '설정한 조건' — 만약에 연결한 조건식
        [System.NonSerialized] public bool lastAmusementPowerCut; // 레벨4 결과의 '놀이시설 끄기 조건(만약)'
        [System.NonSerialized] public bool lastHospitalPowerKept; // 레벨4 결과의 '병원 전력 유지'
        [System.NonSerialized] public bool lastFunctionUsed;      // 레벨5 결과의 '함수 사용'
        [System.NonSerialized] public System.Collections.Generic.List<string> lastEnergiesInFunction; // 레벨5 결과의 에너지별 ON/OFF — 함수 안에 넣은 에너지 이름

        // 컴파일에 성공해 채점까지 끝난 결과인지 — 넘어가기/미완료와 구분한다.
        // 레벨마다 채워지는 값이 달라 개별 필드로 판정하면 분기가 계속 늘고,
        // 값 추출이 실패한 경우(예: 조건식 파싱 실패) 정상 플레이가 스킵으로 잘못 처리된다.
        [System.NonSerialized] public bool hasCodingResult;

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

        /// <summary>
        /// 앱을 껐다 켜면 항상 처음부터 시작하도록 부팅 시점에 진행도를 초기화한다.
        /// </summary>
        public void ResetProgress()
        {
            unlockedLevelIndex = 0;
            currentLevel = null;
            lastQuestionTime = null;
            ResetLastResult();
        }

        /// <summary>
        /// 결과 씬 표시용 값을 초기화한다 — 게임 씬 진입 시, 그리고 넘어가기로 실패 처리할 때.
        /// 문제 값(lastQuestionTime)은 결과 씬에서도 계속 쓰이므로 건드리지 않는다.
        /// </summary>
        public void ResetLastResult()
        {
            lastScore = 0;
            lastDirection = null;
            lastAngle = null;
            lastCount = null;
            lastRepeatUsed = false;
            lastGateHeight = null;
            lastElseUsed = false;
            lastGateOrderCorrect = false;
            lastConditionText = null;
            lastAmusementPowerCut = false;
            lastHospitalPowerKept = false;
            lastFunctionUsed = false;
            lastEnergiesInFunction = null;
            hasCodingResult = false;
        }
    }
}
