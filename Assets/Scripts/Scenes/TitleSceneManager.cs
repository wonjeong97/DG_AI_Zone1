using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DG.Scenes
{
    public class TitleSceneManager : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        private ILogger<TitleSceneManager> _log;

        [Inject]
        public void Construct(ILogger<TitleSceneManager> log)
        {
            _log = log;
        }

        private void Start()
        {
            if (!startButton) _log?.ZLogWarning($"[TitleSceneManager] startButton이 할당되지 않았습니다.");
            
            if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);
        }

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            SceneFader.FadeAndLoad(Constants.Scenes.Intro).Forget();
        }
    }
}
