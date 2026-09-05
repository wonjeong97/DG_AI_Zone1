using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using VContainer;
using VContainer.Unity;
using Wonjeong.App;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace App
{
    public class GameLifetimeScope : RootLifetimeScope
    {
        private GameSession _session;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.Register<VisitorInfoProvider>(Lifetime.Singleton);

            // GameCloser·SystemCanvas 등록은 base의 ConfigureCoreComponents()에서 수행됨(중복 등록 시 VContainer 충돌).
            // 다만 아무도 Resolve하지 않으면 지연 등록만으로는 주입되지 않으므로 빌드 시점에 즉시 Resolve
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<GameCloser>();
                container.Resolve<SystemCanvas>();
            });

            // 전역 페이드 매니저 — App 하위에 생성되어 씬 전환 간 유지
            builder.RegisterComponentOnNewGameObject<FadeManager>(Lifetime.Singleton, "FadeManager")
                .UnderTransform(transform);
            builder.RegisterBuildCallback(container =>
            {
                FadeManager fadeManager = container.Resolve<FadeManager>();

                // 템플릿 FadeManager가 자체 생성하는 FadeCanvas의 sortingOrder가 기본값(-1)이라
                // 씬의 UI Canvas(0)보다도 아래에 그려져 페이드 커튼이 화면을 실제로 가리지 못했음.
                // SystemCanvas(30000)를 포함한 모든 UI 위에 그려지도록 여기서 보정
                Canvas fadeCanvas = fadeManager.GetComponentInChildren<Canvas>(true);
                if (fadeCanvas) fadeCanvas.sortingOrder = 32000;
            });

            // 게임 세션 데이터 — [Inject]로 주입 가능하도록 컨테이너에 등록
            // 앱을 껐다 켜면 항상 처음부터 시작하도록 부팅 시점에 진행도 초기화
            // VContainer Configure는 동기 실행이라 Addressables.WaitForCompletion으로 동기 로드
            _session = Addressables.LoadAssetAsync<GameSession>(Constants.ResourcePaths.GameSessionKey).WaitForCompletion();
            _session.ResetProgress();
            builder.RegisterInstance(_session);

            RegisterTmpFonts();
        }

        /// <summary>
        /// Addressables로 관리하는 TMP 폰트를 MaterialReferenceManager 캐시에 미리 등록한다.
        /// TMP의 &lt;font="..."&gt; 태그는 이 캐시를 먼저 조회하고, 없으면 Resources에서만 폰트를 찾는다.
        /// 등록해 두지 않으면 태그가 해석되지 않고 문자열 그대로 화면에 출력된다.
        /// 첫 씬이 그려지기 전에 끝나야 하므로 동기 로드한다.
        /// </summary>
        private static void RegisterTmpFonts()
        {
            try
            {
                IList<FontAsset> fonts = Addressables
                    .LoadAssetsAsync<FontAsset>(Constants.ResourcePaths.TmpFontLabel, null)
                    .WaitForCompletion();

                if (fonts is null) return;

                foreach (FontAsset font in fonts)
                    if (font) MaterialReferenceManager.AddFontAsset(font);
            }
            catch (Exception ex)
            {
                // 폰트 등록 실패는 치명적이지 않다 — 태그가 해석되지 않을 뿐이므로 부팅은 계속 진행
                Debug.LogWarning($"[GameLifetimeScope] TMP 폰트 등록 실패: {ex.Message}");
            }
        }

        protected override void Awake()
        {
            base.Awake();
            SceneManager.sceneLoaded += OnSceneLoaded;

            // VContainerSettings는 활성 씬이 이미 로드된 상태(에디터에서 특정 씬을 바로 플레이하는 경우 등)면
            // sceneLoaded 이벤트 없이 곧바로 루트 스코프를 생성한다 — 그 최초 씬은 위 구독으로 못 잡으므로 직접 주입
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
                OnSceneLoaded(activeScene, LoadSceneMode.Single);
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        // 새로 로드된 씬의 오브젝트에 [Inject] 필드를 주입.
        // App(DontDestroyOnLoad) 하위는 컨테이너 빌드 시 이미 주입되므로 제외.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene) return;

            // 타이틀로 돌아온 경우(아웃트로 종료 버튼·비활동 타임아웃) 진행도 처리
            if (scene.name == Constants.Scenes.Title)
                HandleReturnToTitleAsync().Forget();

            foreach (GameObject root in scene.GetRootGameObjects())
                Container.InjectGameObject(root);
        }

        /// <summary>
        /// 타이틀로 돌아왔을 때의 진행도 처리 — 서버 사용 여부에 따라 갈린다.
        /// <para>
        /// 서버 미사용: 다음 체험자를 위해 부팅 시점과 동일하게 진행도를 0으로 초기화한다.
        /// 비활동 타임아웃으로 중간에 이탈한 경우에도 레벨1부터 다시 시작하게 된다.
        /// </para>
        /// <para>
        /// 서버 사용: 체험자가 진행도를 저장해 두고 나중에 이어서 할 수 있으므로 로컬에서 일방적으로
        /// 지우면 안 된다. 대신 현재 체험자 세션만 끝내야 한다.
        /// </para>
        /// </summary>
        private async UniTaskVoid HandleReturnToTitleAsync()
        {
            bool isServerConnected;
            try
            {
                isServerConnected = await Container.Resolve<VisitorInfoProvider>()
                    .IsServerConnectedAsync(this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                // 앱 종료·스코프 파괴로 취소된 정상 흐름
                return;
            }
            catch (Exception ex)
            {
                // fire-and-forget이라 여기서 놓치면 UnobservedException으로만 남는다.
                // Visitor.json을 읽지 못하면 서버 미사용(기본값)으로 보고 다음 체험자를 위해 초기화한다.
                Debug.LogWarning($"[GameLifetimeScope] 서버 사용 여부 조회 실패 — 진행도를 초기화합니다: {ex.Message}");
                _session.ResetProgress();
                return;
            }

            if (!isServerConnected)
            {
                _session.ResetProgress();
                return;
            }

            // TODO: 서버 연동 시 — 현재 체험자 식별 정보와 진행도 세션을 종료/초기화할 것.
            //       진행도는 서버에 저장되어 있어 다음 QR 스캔 때 이어서 시작할 수 있어야 하므로,
            //       로컬 ResetProgress로 지우는 대신 "이 체험자의 세션이 끝났다"만 정리해야 한다.
            //       (VisitorInfoProvider.GetNameAsync의 서버 연동 TODO와 함께 구현)
        }
    }
}
