using DG.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class LevelSelectManager : MonoBehaviour
    {
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private LevelData[] levelDataList;
        [SerializeField] private LevelDetailPanel detailPanel;

        private void Start()
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int index = i;
                levelButtons[i].onClick.AddListener(() => OnLevelButtonClicked(index));
            }
        }

        private void OnLevelButtonClicked(int index)
        {
            if (index < levelDataList.Length && levelDataList[index] != null)
                detailPanel.Show(levelDataList[index]);
        }
    }
}
