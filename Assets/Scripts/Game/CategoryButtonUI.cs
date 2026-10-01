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
    }
}
