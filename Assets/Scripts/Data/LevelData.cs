using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "DG/Level Data")]
    public class LevelData : ScriptableObject
    {
        public string nextSceneName;
        public BlockLayoutData blockLayout;

        [Tooltip("2_Story와 3_Game(스토리 다시보기)에서 공통으로 사용하는 레벨 소개 텍스트")]
        [TextArea(3, 10)]
        public string storyText;

        [Tooltip("4_Result에서 '다음' 클릭 시 이동할 씬. 마지막 레벨은 5_Outro로 설정")]
        public string afterResultScene = Constants.Scenes.Story;
    }
}
