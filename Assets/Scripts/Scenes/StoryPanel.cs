using UnityEngine;
using UnityEngine.UI;

namespace DG.Scenes
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

        public void Show(int levelIndex)
        {
            headerImage.sprite = levelHeaderImages[levelIndex];

            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (levelPanels[i]) levelPanels[i].SetActive(i == levelIndex);
            }

            gameObject.SetActive(true);
        }

        private void Hide() => gameObject.SetActive(false);
    }
}
