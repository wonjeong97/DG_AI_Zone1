using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DG.Scenes
{
    public class TitleSceneManager : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        private void Start()
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            SceneManager.LoadScene("1_Intro");
        }
    }
}
