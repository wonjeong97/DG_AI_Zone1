using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Scenes;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.App;
using Wonjeong.Core;
using ZLogger;

namespace App
{
    public class GameManager : GameManagerBase<GameManager>
    {
        private ISubscriber<InactivityTimeoutEvent> _inactivityTimeoutSubscriber;
        private InactivityTimer _inactivityTimer;
        private ILogger<GameManager> _logger;
        private IDisposable _subscription;

        // GameManagerBase 자체 주입과 별도로, 비활동 타임아웃 복귀에 필요한 의존성을 주입
        [Inject]
        public void Construct(ISubscriber<InactivityTimeoutEvent> inactivityTimeoutSubscriber,
            InactivityTimer inactivityTimer, ILogger<GameManager> logger)
        {
            _inactivityTimeoutSubscriber = inactivityTimeoutSubscriber;
            _inactivityTimer = inactivityTimer;
            _logger = logger;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SceneManager.sceneLoaded += OnGameSceneLoaded;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
        }

        protected override void Start()
        {
            base.Start();

            if (_inactivityTimeoutSubscriber != null)
            {
                _subscription = _inactivityTimeoutSubscriber.Subscribe(_ => OnInactivityTimeout());
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[GameManager] inactivityTimeoutSubscriber가 null이라 비활동 타임아웃 복귀가 비활성화됨.");
            }

            // sceneLoaded 이벤트는 구독 이후의 전환만 잡으므로, 부팅 시 이미 로드된 최초 씬은 여기서 직접 반영
            UpdateInactivityTimerState(SceneManager.GetActiveScene().name);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _subscription?.Dispose();
        }

        // 0_Title에서는 타이머를 일시 정지하고, 그 외 씬에서는 재개함
        private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateInactivityTimerState(scene.name);
        }

        private void UpdateInactivityTimerState(string sceneName)
        {
            if (!_inactivityTimer) return;

            if (sceneName == Constants.Scenes.Title)
            {
                _inactivityTimer.Pause();
            }
            else
            {
                _inactivityTimer.Resume();
            }
        }

        /// <summary>
        /// 일정 시간 입력이 없으면 화면 페이드와 함께 타이틀 씬으로 되돌아감.
        /// 이미 타이틀 씬이면(타이머가 일시 정지 상태라 발생하지 않아야 하지만) 아무 동작도 하지 않음.
        /// </summary>
        private void OnInactivityTimeout()
        {
            if (SceneManager.GetActiveScene().name == Constants.Scenes.Title) return;

            SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
        }
    }
}
