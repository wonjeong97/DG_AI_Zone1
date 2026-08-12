using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Wonjeong.UI;
using ZLogger;

namespace DG.Scenes
{
    // 템플릿 FadeManager로 페이드아웃 → 씬 로드 → 페이드인 전환.
    public static class SceneFader
    {
        private static FadeManager _fadeManager;
        private static bool _isLoading;
        private static readonly Queue<UniTask> _pendingTasks = new();

        public static void RegisterPendingTask(UniTask task)
        {
            _pendingTasks.Enqueue(task);
        }

        // isPrepared·frame>=0만으로는 화면에 노출되는 시점이 너무 일러 부자연스러울 수 있어, 재생 진행률이
        // minProgress 이상이 될 때까지 대기함(예: 0.05 = 5% 룰). 재생(Play)이 이미 시작된 VideoPlayer에만 사용할 것
        // — Play가 나중에 호출되는 경우 frame이 계속 -1이라 대기가 끝나지 않음.
        public static async UniTask WaitUntilVideoProgressAsync(VideoPlayer player, float minProgress, CancellationToken ct)
        {
            if (!player) return;

            await UniTask.WaitUntil(() =>
            {
                if (!player.isPrepared || player.frame < 0) return false;
                if (player.frameCount > 0) return player.frame / (float)player.frameCount >= minProgress;
                return player.length > 0 && player.time / player.length >= minProgress;
            }, cancellationToken: ct);

            // frame 값이 갱신되는 시점과 RenderTexture에 실제 픽셀이 반영되는 시점 사이에 한 프레임 지연이 있을 수 있어,
            // FadeIn이 시작되기 전에 화면에 실제로 반영될 시간을 한 프레임 더 확보
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: ct);
        }

        // 씬 전환 직후 VideoPlayer가 사용하는 RenderTexture를 검은색으로 즉시 초기화함.
        // 여러 씬이 같은 RenderTexture 에셋(예: RobotRenderTexture)을 공유하면 이전 씬에서 그려진 마지막 프레임이
        // GPU에 남아있어, 새 영상이 실제로 그리기 전까지 이전 씬 잔상이 잠깐 비칠 수 있음
        public static void ClearVideoRenderTexture(VideoPlayer player)
        {
            if (!player || !player.targetTexture) return;

            RenderTexture rt = player.targetTexture;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        public static async UniTaskVoid FadeAndLoad(string sceneName, float duration = 0.5f, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                FadeManager fade = Find(logger);
                if (fade) await fade.FadeOutAsync(duration);

                // 이전 씬에서 등록됐지만 이 시점까지 대기되지 않은 작업은 폐기됨 — 가시성을 위해 경고 로그
                if (_pendingTasks.Count > 0)
                    logger?.ZLogWarning($"[SceneFader] 이전 씬에서 대기되지 않은 작업 {_pendingTasks.Count}개를 폐기합니다.");
                _pendingTasks.Clear();
                SceneManager.LoadScene(sceneName);

                // 신규 씬의 Awake/Start가 실행되어 대기 작업을 등록할 수 있도록 프레임 끝까지 대기
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

                await AwaitPendingTasks(logger);

                // 모든 비동기 작업(블록 스폰, UI 정렬 등)이 완료된 후, 화면이 완전히 렌더링되고 레이아웃이 정착될 수 있도록 1프레임 더 대기한 뒤 페이드인 시작
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

                if (fade) await fade.FadeInAsync(duration);
            }
            finally
            {
                _isLoading = false;
            }
        }

        // 등록된 대기 작업을 각각 개별 타임아웃으로 대기 — 한 작업의 실패/타임아웃이 나머지 작업 대기를 막지 않도록 함
        private static async UniTask AwaitPendingTasks(Microsoft.Extensions.Logging.ILogger logger)
        {
            // 신규 씬의 Start()가 대기 작업을 등록하기까지 부하 상황에서는 1프레임보다 더 걸릴 수 있어,
            // 큐가 비어있으면 몇 프레임 더 확인한 뒤에야 "등록할 작업이 없다"고 판단함
            for (int i = 0; i < 5 && _pendingTasks.Count == 0; i++)
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            if (_pendingTasks.Count == 0) return;

            logger?.ZLogInformation($"[SceneFader] {_pendingTasks.Count}개의 대기 작업 완료를 기다립니다...");
            while (_pendingTasks.Count > 0)
            {
                UniTask task = _pendingTasks.Dequeue();
                try
                {
                    await task.Timeout(TimeSpan.FromSeconds(5f));
                }
                catch (Exception ex)
                {
                    logger?.ZLogWarning($"[SceneFader] 대기 작업 중 예외 또는 타임아웃 발생 ({ex.Message}). 다음 작업으로 계속합니다.");
                }
            }
            logger?.ZLogInformation($"[SceneFader] 대기 작업 완료.");
        }

        // GameLifetimeScope가 App 하위에 생성한 전역 인스턴스 조회 (없으면 페이드 없이 로드)
        private static FadeManager Find(Microsoft.Extensions.Logging.ILogger logger)
        {
            if (_fadeManager) return _fadeManager;

            _fadeManager = UnityEngine.Object.FindObjectOfType<FadeManager>();
            if (!_fadeManager)
                logger?.ZLogWarning($"[SceneFader] FadeManager를 찾을 수 없습니다. 페이드 없이 씬을 전환합니다.");
            return _fadeManager;
        }

        // ── CanvasGroup 페이드 공용 헬퍼 (씬 매니저 간 중복 구현 통합) ──────────

        // 그룹의 alpha를 from→to로 보간
        public static async UniTask FadeCanvasGroupAsync(CanvasGroup group, float from, float to, float duration, CancellationToken ct)
        {
            if (!group) return;

            group.alpha = from;
            await group.DOFade(to, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true) // 씬 전환 페이드는 timeScale 0에서도 동작해야 하는 시스템 연출
                .SetLink(group.gameObject)
                .ToUniTask(cancellationToken: ct);
        }

        // 두 CanvasGroup 간 크로스페이드 — interactable/blocksRaycasts 전환 포함
        public static async UniTask CrossFadeGroupsAsync(CanvasGroup from, CanvasGroup to, float duration, CancellationToken ct)
        {
            SetGroupInteractable(to, true);

            UniTask fadeOut = from
                ? from.DOFade(0f, duration).SetEase(Ease.Linear).SetUpdate(true).SetLink(from.gameObject).ToUniTask(cancellationToken: ct)
                : UniTask.CompletedTask;
            UniTask fadeIn = to
                ? to.DOFade(1f, duration).SetEase(Ease.Linear).SetUpdate(true).SetLink(to.gameObject).ToUniTask(cancellationToken: ct)
                : UniTask.CompletedTask;
            await UniTask.WhenAll(fadeOut, fadeIn);

            SetGroupInteractable(from, false);
        }

        public static void SetGroupInteractable(CanvasGroup group, bool value)
        {
            if (!group) return;
            group.interactable = value;
            group.blocksRaycasts = value;
        }
    }
}
