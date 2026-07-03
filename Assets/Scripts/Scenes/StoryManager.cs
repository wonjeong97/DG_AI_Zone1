using DG.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class StoryManager : MonoBehaviour
    {
        [SerializeField] private LevelData[] levelDataList;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private Image headerImage;
        [SerializeField] private Text storyText;
        [SerializeField] private Button startButton;

        private LevelData _currentLevel;

        private void Start()
        {
            int index = GameSession.Instance?.unlockedLevelIndex ?? 0;
            _currentLevel = levelDataList[index];

            headerImage.sprite = levelHeaderImages[index];
            storyText.text = _currentLevel.storyText;

            startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            GameSession.Instance.currentLevel = _currentLevel;
            SceneManager.LoadScene(_currentLevel.nextSceneName);
        }
    }
}
