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

        // levelName: 현재 플레이 중인 레벨의 LevelData 에셋 이름 (예: "02_WindData").
        // 진행도(unlockedLevelIndex)가 아닌 실제 로드된 레벨을 기준으로 패널을 골라야
        // testLevel로 특정 레벨을 단독 테스트할 때도 올바른 스토리 패널이 열린다.
        // storyText가 null이면 패널에 이미 있는 텍스트를 그대로 둠(테스트 등 LevelData 없이 호출하는 경우 대비)
        public void Show(string levelName, string storyText = null)
        {
            int levelNumber = Constants.Levels.ParseLevelNumber(levelName);
            string targetSpriteName = levelNumber > 0 ? $"Level{levelNumber}" : null;
            string targetPanelName = levelNumber > 0 ? $"Level{levelNumber}Panel" : null;

            if (headerImage && levelHeaderImages != null)
            {
                foreach (Sprite sprite in levelHeaderImages)
                {
                    if (sprite && sprite.name == targetSpriteName)
                    {
                        headerImage.sprite = sprite;
                        headerImage.SetNativeSize();
                        break;
                    }
                }
            }

            GameObject targetPanel = null;
            foreach (GameObject panel in levelPanels)
            {
                if (!panel) continue;
                bool match = panel.name == targetPanelName;
                panel.SetActive(match);
                if (match) targetPanel = panel;
            }

            // 2_Story와 같은 텍스트를 쓰도록 LevelData.storyText에서 가져와 Text_LevelNStory에 적용
            if (storyText != null && targetPanel)
            {
                string targetTextName = $"Text_Level{levelNumber}Story";
                foreach (TMP_Text tmp in targetPanel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.name == targetTextName)
                    {
                        tmp.text = storyText;
                        break;
                    }
                }
            }

            gameObject.SetActive(true);
        }

        private void Hide() => gameObject.SetActive(false);
    }
}
