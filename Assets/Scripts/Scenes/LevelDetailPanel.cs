using DG.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class LevelDetailPanel : MonoBehaviour
    {
        [SerializeField] private Image headerImage;
        [SerializeField] private Text storyText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        private LevelData _currentLevel;

        private void Start()
        {
            startButton.onClick.AddListener(OnStartClicked);
            backButton.onClick.AddListener(OnBackClicked);
            gameObject.SetActive(false);
        }

        public void Show(LevelData data)
        {
            _currentLevel = data;
            headerImage.sprite = data.headerImage;
            storyText.text = data.storyText;
            gameObject.SetActive(true);
        }

        private void OnStartClicked()
        {
            GameSession.Instance.currentLevel = _currentLevel;
            SceneManager.LoadScene(_currentLevel.nextSceneName);
        }
        private void OnBackClicked() => gameObject.SetActive(false);
    }
}
