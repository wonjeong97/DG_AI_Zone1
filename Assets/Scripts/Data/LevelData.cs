using UnityEngine;

namespace DG.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "DG/Level Data")]
    public class LevelData : ScriptableObject
    {
        public Sprite headerImage;
        [TextArea(5, 15)] public string storyText;
        public string nextSceneName;
        public BlockLayoutData blockLayout;
    }
}
