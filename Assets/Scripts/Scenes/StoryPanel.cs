using UnityEngine;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class StoryPanel : MonoBehaviour
    {
        [SerializeField] private Image headerImage;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private Text storyText;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            closeButton.onClick.AddListener(Hide);
            gameObject.SetActive(false);
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
