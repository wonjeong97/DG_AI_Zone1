using UnityEngine;

namespace Data
{
    // 레벨 종류 — 문제 출제·채점·결과 연출이 이 값으로 분기한다 (에셋 이름을 바꿔도 동작이 바뀌지 않도록)
    public enum LevelKind
    {
        Solar,          // 레벨1 태양광
        Wind,           // 레벨2 풍력
        Hydro,          // 레벨3 수력
        PowerPlant,     // 레벨4 발전소
        FutureEnergy    // 레벨5 미래에너지 — 채점은 발전소 규칙을 공유
    }

    [CreateAssetMenu(fileName = "LevelData", menuName = "DG/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("진행도 판정용 순번 — StoryManager.levelDataList 내 위치와 일치해야 함 (레벨1=0, 레벨2=1, ...)")]
        public int levelIndex;

        [Tooltip("레벨 종류 — 문제 출제·채점·결과 연출 분기 기준")]
        public LevelKind kind;

        public BlockLayoutData blockLayout;

        [Tooltip("2_Story와 3_Game(스토리 다시보기)에서 공통으로 사용하는 레벨 소개 텍스트")]
        [TextArea(3, 10)]
        public string storyText;

        [Tooltip("4_Result 상단 안내 문구 — 뒤의 말줄임(점 3개)은 코드가 붙이므로 빼고 입력. 비우면 씬에 입력해둔 텍스트를 유지")]
        public string resultTopText;

        [Header("문제")]
        [Tooltip("3_Game 문제 문구 — {0}에 문제 값이 들어간다(<color> 태그 사용 가능). 문제 값이 하나뿐인 고정 문제는 {0} 없이 써도 된다")]
        [TextArea(3, 6)]
        public string questionFormat;

        [Tooltip("문제 값 후보 — 매 판 하나를 무작위로 고른다(하나만 두면 고정 문제)")]
        public QuestionOption[] questionOptions;

        [Tooltip("3_Game 힌트 패널의 규칙 문구 — {0}에 문제 값이 들어간다(레벨3 수력). 비우면 쓰지 않는다")]
        [TextArea(2, 4)]
        public string hintRuleFormat;

        // 씬 이름을 에셋에 직렬화하면 씬 리네임 시 낡은 값이 남으므로, 여부만 저장하고 씬 이름은 Constants에서 고른다
        [Tooltip("체크하면 4_Result에서 '다음' 클릭 시 5_Outro로, 아니면 2_Story로 이동 (마지막 레벨에서 체크)")]
        public bool goToOutroAfterResult;

        public string AfterResultScene => goToOutroAfterResult ? Constants.Scenes.Outro : Constants.Scenes.Story;

        /// <summary>
        /// 문제 값 후보 중 하나를 무작위로 고른다 (후보가 없으면 null).
        /// </summary>
        public QuestionOption PickQuestionOption()
        {
            if (questionOptions is null || questionOptions.Length == 0) return null;
            return questionOptions[Random.Range(0, questionOptions.Length)];
        }

        /// <summary>
        /// 문제 값에 해당하는 후보를 찾는다 (없으면 null).
        /// </summary>
        public QuestionOption FindQuestionOption(string value)
        {
            if (questionOptions is null || string.IsNullOrEmpty(value)) return null;

            foreach (QuestionOption option in questionOptions)
                if (option is not null && option.value == value) return option;
            return null;
        }

        /// <summary>
        /// 문제 값에 대한 정답 블록 라벨을 반환한다 (정답 방향이 없는 문제면 null).
        /// </summary>
        public string GetCorrectAnswer(string value)
        {
            QuestionOption option = FindQuestionOption(value);
            return option is not null && !string.IsNullOrEmpty(option.correctAnswer) ? option.correctAnswer : null;
        }
    }

    // 문제 값 하나 — 문제 문구의 {0}에 들어가는 값과, 그 값일 때의 정답·힌트 그림
    [System.Serializable]
    public class QuestionOption
    {
        [Tooltip("문제 값 — 문제 문구의 {0}에 들어가고 채점·결과·힌트의 기준이 된다(예: 아침 8시, 동쪽, 5m). 수력 높이는 블록 라벨('5m 이상')의 앞자리 숫자와 맞춘다")]
        public string value;

        [Tooltip("정답 블록 라벨 — 방향 문제에서만 쓴다(예: 동쪽). 블록 레이아웃의 라벨과 같아야 한다. 비우면 정답 방향 비교를 하지 않는다")]
        public string correctAnswer;

        [Tooltip("이 값일 때 3_Game 힌트 패널에서 켤 그림 오브젝트 이름(예: 8AM, Image_Wind_East). 비우면 힌트 그림을 바꾸지 않는다")]
        public string hintObjectName;
    }
}
