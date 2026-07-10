using Cysharp.Threading.Tasks;
using DG.Data;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace DG.Scenes
{
    public class StoryManager : MonoBehaviour
    {
        [SerializeField] private LevelData[] levelDataList;
        [SerializeField] private Sprite[] levelHeaderImages;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject storyPanel;
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private Image headerImage;
        [SerializeField] private Text storyText;
        [SerializeField] private Button startButton;

        [Inject] private GameSession _session;

        private LevelData _currentLevel;

        private void Start()
        {
            if (storyPanel) storyPanel.SetActive(false);
            if (levelSelectPanel) levelSelectPanel.SetActive(true);

            int unlockedIndex = Mathf.Clamp(_session ? _session.unlockedLevelIndex : 0, 0, levelDataList.Length - 1);

            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (!levelButtons[i]) continue;

                int btnIndex = i;
                levelButtons[i].onClick.AddListener(() => OnLevelButtonClicked(btnIndex));

                if (i == unlockedIndex)
                {
                    levelButtons[i].interactable = true;
                    if (levelButtons[i].image) levelButtons[i].image.material = null;
                }
                else
                {
                    levelButtons[i].interactable = false;
                    if (levelButtons[i].image)
                        levelButtons[i].image.material = UiEffects.GrayscaleMaterial;
                }
            }

            startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnLevelButtonClicked(int index)
        {
            if (index < 0 || index >= levelDataList.Length) return;

            _currentLevel = levelDataList[index];
            if (headerImage && index < levelHeaderImages.Length)
                headerImage.sprite = levelHeaderImages[index];
            if (storyText && _currentLevel != null)
                storyText.text = _currentLevel.storyText;

            if (levelSelectPanel) levelSelectPanel.SetActive(false);
            if (storyPanel) storyPanel.SetActive(true);
        }

        private void OnStartClicked()
        {
            if (_currentLevel == null) return;
            if (_session) _session.currentLevel = _currentLevel;
            SceneFader.FadeAndLoad(_currentLevel.nextSceneName).Forget();
        }
    }
}
