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

        // 씬 이름을 에셋에 직렬화하면 씬 리네임 시 낡은 값이 남으므로, 여부만 저장하고 씬 이름은 Constants에서 고른다
        [Tooltip("체크하면 4_Result에서 '다음' 클릭 시 5_Outro로, 아니면 2_Story로 이동 (마지막 레벨에서 체크)")]
        public bool goToOutroAfterResult;

        public string AfterResultScene => goToOutroAfterResult ? Constants.Scenes.Outro : Constants.Scenes.Story;
    }
}
