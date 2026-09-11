using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "DG/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("진행도 판정용 순번 — StoryManager.levelDataList 내 위치와 일치해야 함 (레벨1=0, 레벨2=1, ...)")]
        public int levelIndex;

        public string nextSceneName;
        public BlockLayoutData blockLayout;

        [Tooltip("2_Story와 3_Game(스토리 다시보기)에서 공통으로 사용하는 레벨 소개 텍스트")]
        [TextArea(3, 10)]
        public string storyText;

        [Tooltip("4_Result 상단 안내 문구 — 뒤의 말줄임(점 3개)은 코드가 붙이므로 빼고 입력. 비우면 씬에 입력해둔 텍스트를 유지")]
        public string resultTopText;

        [Tooltip("4_Result에서 '다음' 클릭 시 이동할 씬. 마지막 레벨은 5_Outro로 설정")]
        public string afterResultScene = Constants.Scenes.Story;
    }
}
