using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.UI;
using ZLogger;

namespace DG.Scenes
{
    // 템플릿 FadeManager로 페이드아웃 → 씬 로드 → 페이드인 전환.
    public static class SceneFader
    {
        private static FadeManager _fadeManager;
        private static bool _isLoading;

        public static async UniTaskVoid FadeAndLoad(string sceneName, float duration = 0.5f, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                FadeManager fade = Find(logger);
                if (fade) await fade.FadeOutAsync(duration);
                SceneManager.LoadScene(sceneName);
                if (fade) await fade.FadeInAsync(duration);
            }
            finally
            {
                _isLoading = false;
            }
        }

        // GameLifetimeScope가 App 하위에 생성한 전역 인스턴스 조회 (없으면 페이드 없이 로드)
        private static FadeManager Find(Microsoft.Extensions.Logging.ILogger logger)
        {
            if (_fadeManager) return _fadeManager;

            _fadeManager = Object.FindObjectOfType<FadeManager>();
            if (!_fadeManager)
                logger?.ZLogWarning($"[SceneFader] FadeManager를 찾을 수 없습니다. 페이드 없이 씬을 전환합니다.");
            return _fadeManager;
        }
    }
}
