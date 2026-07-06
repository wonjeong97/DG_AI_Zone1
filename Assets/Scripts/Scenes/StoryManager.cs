using DG.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace DG.Scenes
{
    public class StoryManager : MonoBehaviour
    {
        [SerializeField] private LevelData[] levelDataList;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private Image headerImage;
        [SerializeField] private Text storyText;
        [SerializeField] private Button startButton;

        [Inject] private GameSession _session;

        private LevelData _currentLevel;

        private void Start()
        {
            // 마지막 레벨 완료 후에도 unlockedLevelIndex가 배열 범위를 넘지 않도록 고정
            int index = Mathf.Clamp(_session ? _session.unlockedLevelIndex : 0, 0, levelDataList.Length - 1);
            _currentLevel = levelDataList[index];

            headerImage.sprite = levelHeaderImages[index];
            storyText.text = _currentLevel.storyText;

            startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            _session.currentLevel = _currentLevel;
            SceneManager.LoadScene(_currentLevel.nextSceneName);
        }
    }
}
