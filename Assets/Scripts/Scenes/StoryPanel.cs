using Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace Scenes
{
    public class StoryPanel : MonoBehaviour
    {
        [SerializeField] private Image headerImage;
        [Tooltip("레벨 순서대로(레벨1=0번) 스토리 헤더에 표시할 이미지")]
        [SerializeField] private Sprite[] levelHeaderImages;
        [Tooltip("레벨 순서대로(레벨1=0번) 각 레벨의 스토리 패널 — LevelData.levelIndex로 고른다")]
        [SerializeField] private GameObject[] levelPanels;
        [Tooltip("레벨 순서대로(레벨1=0번), 각 레벨 패널 안에서 LevelData.storyText를 표시할 텍스트(Text_LevelNStory)")]
        [SerializeField] private TMP_Text[] levelStoryTexts;
        [SerializeField] private Button closeButton;

        private ILogger<StoryPanel> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거와 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<StoryPanel> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 닫기 버튼에 숨기기 동작을 연결한다.
        /// </summary>
        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// 현재 레벨의 헤더 이미지와 스토리 패널을 켜고 LevelData의 스토리 문구를 채운다.
        /// level은 현재 플레이 중인 레벨로, 진행도가 아닌 실제 로드된 레벨을 기준으로 골라야 testLevel 단독 테스트에서도 올바른 패널이 열린다.
        /// 헤더 이미지·패널·스토리 텍스트 배열은 모두 레벨 순서(레벨1=0번)라 LevelData.levelIndex로 고른다.
        /// </summary>
        public void Show(LevelData level)
        {
            int index = level ? level.levelIndex : -1;

            Sprite header = ItemAt(levelHeaderImages, index);
            if (headerImage && header)
            {
                headerImage.sprite = header;
                headerImage.SetNativeSize();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[StoryPanel] 레벨 순번 {index}의 헤더 이미지가 할당되지 않았습니다.");
            }

            GameObject targetPanel = ItemAt(levelPanels, index);
            for (int i = 0; i < levelPanels.Length; i++)
                if (levelPanels[i]) levelPanels[i].SetActive(i == index);

            if (!targetPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[StoryPanel] 레벨 순번 {index}의 스토리 패널이 levelPanels에 없어 스토리를 표시하지 못했습니다.");
            }
            else
            {
                // 2_Story와 같은 텍스트를 쓰도록 LevelData.storyText에서 가져와 Text_LevelNStory에 적용
                TMP_Text target = ItemAt(levelStoryTexts, index);
                if (target)
                    target.text = level.storyText;
                else if (_logger != null)
                    _logger.ZLogWarning($"[StoryPanel] 레벨 순번 {index}의 levelStoryTexts가 할당되지 않아 스토리 문구를 표시하지 않습니다.");
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 배열에서 인덱스의 항목을 반환한다 (배열이 없거나 범위를 벗어나면 null).
        /// </summary>
        private static T ItemAt<T>(T[] items, int index) where T : Object
            => items is not null && index >= 0 && index < items.Length ? items[index] : null;

        /// <summary>
        /// 닫기 버튼 클릭음을 내고 스토리 패널을 닫는다.
        /// </summary>
        private void Hide()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            gameObject.SetActive(false);
        }
    }
}
