using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class OutroSceneManager : MonoBehaviour
    {
        [SerializeField] private Button endButton;

        private void Start()
        {
            if (endButton)
                endButton.onClick.AddListener(OnEndButtonClicked);
        }

        private void OnDestroy()
        {
            if (endButton)
                endButton.onClick.RemoveListener(OnEndButtonClicked);
        }

        private void OnEndButtonClicked()
        {
            SceneFader.FadeAndLoad("0_Title").Forget();
        }
    }
}
