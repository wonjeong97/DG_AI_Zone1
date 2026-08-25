using System.Collections.Generic;
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

        private static readonly Dictionary<string, string> TimeVariantNames = new()
        {
            ["아침 8시"]  = "8AM",
            ["오전 10시"] = "10AM",
            ["정오"]      = "12PM",
            ["오후 2시"]  = "2PM",
            ["오후 4시"]  = "4PM",
        };

        private void Awake()
        {
            if (closeButton)
                closeButton.onClick.AddListener(Hide);
        }

        public void Show(int levelIndex, string questionValueKey = null)
        {
            for (int i = 0; i < levelPanels.Length; i++)
            {
                if (levelPanels[i]) levelPanels[i].SetActive(i == levelIndex);
            }

            if (level1TimeContainer)
                level1TimeContainer.gameObject.SetActive(levelIndex == 0);

            if (levelIndex == 0)
                ApplyTimeVariant(questionValueKey);

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

        private void Hide() => gameObject.SetActive(false);
    }
}
