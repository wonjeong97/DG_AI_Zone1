using UnityEngine;

namespace DG.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "DG/Level Data")]
    public class LevelData : ScriptableObject
    {
        [TextArea(5, 15)] public string storyText;
        public string nextSceneName;
        public BlockLayoutData blockLayout;

        [Tooltip("4_Result에서 '다음' 클릭 시 이동할 씬. 마지막 레벨은 5_Outro로 설정")]
        public string afterResultScene = "2_Story";
    }
}
