using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scenes
{
    public class StoryPanel : MonoBehaviour
    {
        [SerializeField] private Image headerImage;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private GameObject[] levelPanels;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        // storyText가 null이면 패널에 이미 있는 텍스트를 그대로 둠(테스트 등 LevelData 없이 호출하는 경우 대비)
        public void Show(int levelIndex, string storyText = null)
        {
            headerImage.sprite = levelHeaderImages[levelIndex];

            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (levelPanels[i]) levelPanels[i].SetActive(i == levelIndex);
            }

            // 2_Story와 같은 텍스트를 쓰도록 LevelData.storyText에서 가져옴
            if (storyText != null && levelIndex < levelPanels.Length && levelPanels[levelIndex])
            {
                TMP_Text tmp = levelPanels[levelIndex].GetComponentInChildren<TMP_Text>(true);
                if (tmp) tmp.text = storyText;
            }

            gameObject.SetActive(true);
        }

        private void Hide() => gameObject.SetActive(false);
    }
}
