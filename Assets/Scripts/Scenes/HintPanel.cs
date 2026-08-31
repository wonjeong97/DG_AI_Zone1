using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scenes
{
    public class HintPanel : MonoBehaviour
    {
        [SerializeField] private GameObject[] levelPanels;
        [SerializeField] private Button closeButton;

        [Tooltip("레벨1 힌트의 시간대별(8AM/10AM/12PM/2PM/4PM) 오브젝트를 담은 부모 — 문제 시간에 맞는 것만 표시")]
        [SerializeField] private Transform level1TimeContainer;

        [Tooltip("레벨2 힌트의 풍향별(Image_Wind_East/West/South/North) 오브젝트를 담은 부모(Level2Panel) — 문제 풍향에 맞는 것만 표시")]
        [SerializeField] private Transform level2WindContainer;

        [Tooltip("레벨3(수력) 힌트의 강물 높이 텍스트(Text_Meter) — 문제 높이 값을 그대로 표시")]
        [SerializeField] private TMP_Text level3MeterText;

        [Tooltip("레벨4(발전소) 힌트 오브젝트를 담은 부모(Level4Panel) — 문제 변형별 토글 로직 추가 시 사용")]
        [SerializeField] private Transform level4PowerPlantContainer;

        [Tooltip("레벨5(미래 에너지) 힌트 오브젝트를 담은 부모(Level5Panel) — 문제 변형별 토글 로직 추가 시 사용")]
        [SerializeField] private Transform level5FutureEnergyContainer;

        private static readonly Dictionary<string, string> TimeVariantNames = new()
        {
            ["아침 8시"]  = "8AM",
            ["오전 10시"] = "10AM",
            ["정오"]      = "12PM",
            ["오후 2시"]  = "2PM",
            ["오후 4시"]  = "4PM",
        };

        private static readonly Dictionary<string, string> WindVariantNames = new()
        {
            [Constants.Directions.East]  = "Image_Wind_East",
            [Constants.Directions.West]  = "Image_Wind_West",
            [Constants.Directions.South] = "Image_Wind_South",
            [Constants.Directions.North] = "Image_Wind_North",
        };

        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        // levelName: 현재 플레이 중인 레벨의 LevelData 에셋 이름 (예: "02_WindData").
        // 진행도(unlockedLevelIndex)가 아닌 실제 로드된 레벨을 기준으로 패널을 골라야
        // testLevel로 특정 레벨을 단독 테스트할 때도 올바른 힌트 패널이 열린다.
        public void Show(string levelName, string questionValueKey = null)
        {
            int levelNumber = Constants.Levels.ParseLevelNumber(levelName);
            string targetPanelName = levelNumber > 0 ? $"Level{levelNumber}Panel" : null;

            foreach (GameObject panel in levelPanels)
            {
                if (panel) panel.SetActive(panel.name == targetPanelName);
            }

            if (level1TimeContainer)
                level1TimeContainer.gameObject.SetActive(level1TimeContainer.gameObject.name == targetPanelName);

            if (targetPanelName == "Level1Panel")
                ApplyTimeVariant(questionValueKey);

            if (targetPanelName == "Level2Panel")
                ApplyWindVariant(questionValueKey);

            if (targetPanelName == "Level3Panel")
                ApplyMeterText(questionValueKey);

            gameObject.SetActive(true);
        }

        // 레벨1 힌트 패널의 시간대 오브젝트 중 현재 문제의 시간과 일치하는 것만 활성화
        private void ApplyTimeVariant(string questionValueKey)
        {
            if (!level1TimeContainer) return;

            string target = questionValueKey != null && TimeVariantNames.TryGetValue(questionValueKey, out string name) ? name : null;
            foreach (Transform child in level1TimeContainer)
            {
                // 시간대 오브젝트만 토글 — Image_Horizon 등 공통 배경은 그대로 둠
                if (TimeVariantNames.ContainsValue(child.name))
                    child.gameObject.SetActive(child.name == target);
            }
        }

        // 레벨2 힌트 패널의 풍향 오브젝트 중 현재 문제의 바람 방향과 일치하는 것만 활성화
        private void ApplyWindVariant(string questionValueKey)
        {
            if (!level2WindContainer) return;

            string target = questionValueKey != null && WindVariantNames.TryGetValue(questionValueKey, out string name) ? name : null;
            foreach (Transform child in level2WindContainer)
            {
                // 풍향 오브젝트만 토글 — Image_Windforce 등 공통 배경은 그대로 둠
                if (WindVariantNames.ContainsValue(child.name))
                    child.gameObject.SetActive(child.name == target);
            }
        }

        // 레벨3 힌트 패널의 Text_Meter에 현재 문제의 강물 높이 값을 그대로 표시
        private void ApplyMeterText(string questionValueKey)
        {
            if (level3MeterText)
                level3MeterText.text = questionValueKey;
        }

        private void Hide() => gameObject.SetActive(false);
    }
}
