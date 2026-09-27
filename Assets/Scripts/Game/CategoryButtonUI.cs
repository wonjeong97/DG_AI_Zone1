using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game
{
    public class CategoryButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI labelText;

        public Button Button => button;
        public Image FillImage => fillImage;
        public TextMeshProUGUI LabelText => labelText;

        /// <summary>
        /// 코드로 버튼을 조립한 경우 버튼·채움 이미지·라벨 참조를 연결한다.
        /// </summary>
        public void SetReferences(Button btn, Image fill, TextMeshProUGUI label)
        {
            button = btn;
            fillImage = fill;
            labelText = label;
        }
    }
}
