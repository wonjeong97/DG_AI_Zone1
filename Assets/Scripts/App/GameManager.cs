using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Scenes;
using UnityEngine.SceneManagement;
using VContainer;
using HuliacDev.App;
using HuliacDev.Core;
using ZLogger;

namespace App
{
    public class GameManager : GameManagerBase
    {
        private ISubscriber<InactivityTimeoutEvent> _inactivityTimeoutSubscriber;
        private InactivityTimer _inactivityTimer;
        private ILogger<GameManager> _logger;
        private IDisposable _subscription;

        /// <summary>
        /// GameManagerBase 자체 주입과 별도로, 비활동 타임아웃 복귀에 필요한 의존성을 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ISubscriber<InactivityTimeoutEvent> inactivityTimeoutSubscriber,
            InactivityTimer inactivityTimer, ILogger<GameManager> logger)
        {
            _inactivityTimeoutSubscriber = inactivityTimeoutSubscriber;
            _inactivityTimer = inactivityTimer;
            _logger = logger;
        }

        /// <summary>
        /// 씬 로드 이벤트를 구독해 씬마다 비활동 타이머 상태를 맞춘다.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            SceneManager.sceneLoaded += OnGameSceneLoaded;
        }

        /// <summary>
        /// 씬 로드 이벤트 구독을 해제한다.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
        }

        /// <summary>
        /// 비활동 타임아웃 이벤트를 구독하고 최초 씬의 타이머 상태를 반영한다.
        /// </summary>
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

        /// <summary>
        /// 비활동 타임아웃 구독을 해제한다.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            _subscription?.Dispose();
        }

        /// <summary>
        /// 새로 로드된 씬에 맞춰 비활동 타이머를 일시 정지하거나 재개한다.
        /// </summary>
        private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateInactivityTimerState(scene.name);
        }

        /// <summary>
        /// 0_Title에서는 비활동 타이머를 일시 정지하고, 그 외 씬에서는 재개한다.
        /// </summary>
        private void UpdateInactivityTimerState(string sceneName)
        {
            if (!_inactivityTimer)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameManager] InactivityTimer가 주입되지 않아 타이머 상태를 바꾸지 못했습니다.");
                return;
            }

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
        /// 일정 시간 입력이 없으면 화면 페이드와 함께 타이틀 씬으로 되돌아간다.
        /// </summary>
        private void OnInactivityTimeout()
        {
            // 이미 타이틀 씬이면(타이머가 일시 정지 상태라 발생하지 않아야 하지만) 아무 동작도 하지 않는다.
            if (SceneManager.GetActiveScene().name == Constants.Scenes.Title) return;

            SceneFader.FadeAndLoad(Constants.Scenes.Title, logger: _logger).Forget();
        }
    }
}
