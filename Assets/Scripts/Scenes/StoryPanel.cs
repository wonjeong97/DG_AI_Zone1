using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DG.Scenes
{
    public class StoryPanel : MonoBehaviour
    {
        [SerializeField] private Image headerImage;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private TextMeshProUGUI storyText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        public void Show(int levelIndex, string text)
        {
            headerImage.sprite = levelHeaderImages[levelIndex];
            storyText.text = text;
            gameObject.SetActive(true);
        }

        private void Hide() => gameObject.SetActive(false);
    }
}
