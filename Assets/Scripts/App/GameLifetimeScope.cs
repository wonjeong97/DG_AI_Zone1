using System;
using System.Collections.Generic;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using HuliacDev.App;
using HuliacDev.UI;
using HuliacDev.Utils;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace App
{
    public class GameLifetimeScope : RootLifetimeScope
    {
        // SystemCanvas(30000)보다 위
        private const int FadeSortingOrder = 32000;

        /// <summary>
        /// 템플릿 기본 등록에 더해 게임 서비스·전역 페이드·TMP 폰트를 등록하고 템플릿 컴포넌트 설정을 맞춘다.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            ConfigureGameServices(builder);
            ConfigureTemplateComponents(builder);
            ConfigureFade(builder);

            // 폰트 등록 실패를 ZLogger로 남기기 위해 로거를 받을 수 있는 빌드 콜백에서 수행한다.
            // 빌드 콜백도 루트 스코프 Awake 안에서 실행되므로 첫 씬이 그려지기 전에 끝난다.
            builder.RegisterBuildCallback(container =>
                RegisterTmpFonts(container.Resolve<ILogger<GameLifetimeScope>>()));
        }

        /// <summary>
        /// 게임 매니저, 체험자 정보·서버 API, 게임 세션, 체험자 설정을 등록한다.
        /// </summary>
        private static void ConfigureGameServices(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.Register<VisitorInfoProvider>(Lifetime.Singleton);

            // 관리자 창이 열려 있는지 — 타이틀이 그동안 찍힌 QR을 서버로 보내지 않게 한다
            builder.Register<Admin.AdminScreenState>(Lifetime.Singleton);
            builder.Register<Network.VisitorApiClient>(Lifetime.Singleton);

            // 한 판의 진행 상태 — 앱을 켤 때마다 새 인스턴스로 시작하므로 처음부터 시작한다
            builder.Register<GameSession>(Lifetime.Singleton);

            // 운영 모드·체험자 이름 — 관리자 페이지에서 바꾼 값은 PlayerPrefs에 남아 있어 재부팅 후에도 유지된다
            builder.RegisterInstance(LoadVisitorSettings());
        }

        /// <summary>
        /// 템플릿 컴포넌트를 즉시 만든다.
        /// </summary>
        private static void ConfigureTemplateComponents(IContainerBuilder builder)
        {
            // GameCloser·SystemCanvas 등록은 base의 ConfigureCoreComponents()에서 수행됨(중복 등록 시 VContainer 충돌).
            // 다만 아무도 Resolve하지 않으면 지연 등록만으로는 주입되지 않으므로 빌드 시점에 즉시 Resolve
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<GameCloser>();
                container.Resolve<SystemCanvas>();
            });
        }

        /// <summary>
        /// 템플릿 단축키(D·I·M·F)를 Ctrl 조합으로 바꾼다 — QR 스캐너가 입력하는 uid 문자와 겹치지 않게 한다.
        /// </summary>
        protected override void ConfigureInputBindings(TemplateInputActions inputActions)
        {
            // 템플릿이 입력 액션을 만든 직후 어떤 소비자가 켜기 전에 불러 준다
            DebugShortcutBindings.Apply(inputActions);
        }

        /// <summary>
        /// 씬 전환에 쓰는 전역 페이드 매니저를 App 하위에 만들고 SceneFader에 넘긴다.
        /// </summary>
        private void ConfigureFade(IContainerBuilder builder)
        {
            // 전역 페이드 매니저 — App 하위에 생성되어 씬 전환 간 유지
            builder.RegisterComponentOnNewGameObject<FadeManager>(Lifetime.Singleton, "FadeManager")
                .UnderTransform(transform);
            builder.RegisterBuildCallback(container =>
            {
                FadeManager fadeManager = container.Resolve<FadeManager>();
                Scenes.SceneFader.RegisterFadeManager(fadeManager);

                // 페이드 커튼이 SystemCanvas(30000)를 포함한 모든 UI 위에 그려지도록 페이드 중 sortingOrder를 지정
                // (템플릿 기본값 999는 SystemCanvas보다 아래)
                fadeManager.SetSortingOrder(FadeSortingOrder);
            });
        }

        /// <summary>
        /// 체험자 설정(VisitorSettings SO)을 Addressables로 불러온다.
        /// </summary>
        private static VisitorSettings LoadVisitorSettings()
        {
            // Configure는 동기 실행이라 WaitForCompletion으로 동기 로드한다.
            try
            {
                VisitorSettings settings = Addressables.LoadAssetAsync<VisitorSettings>(Constants.ResourcePaths.VisitorSettingsKey).WaitForCompletion();
                if (settings) return settings;
            }
            catch (Exception ex)
            {
                // 컨테이너 구성 도중이라 로거를 아직 주입받을 수 없어 Debug로 남긴다
                Debug.LogError($"[GameLifetimeScope] VisitorSettings 로드 중 예외: {ex.Message}");
            }

            // 불러오지 못하면 null이 등록돼 루트 빌드가 깨지지 않도록, 에셋 기본값과 같은 임시 인스턴스로 대체하고 에러를 남긴다
            // (관리자 페이지에서 바꾼 PlayerPrefs 값은 그대로 읽힌다).
            Debug.LogError($"[GameLifetimeScope] Addressables 주소 '{Constants.ResourcePaths.VisitorSettingsKey}'의 VisitorSettings를 불러오지 못해 기본값으로 대체합니다.");
            return ScriptableObject.CreateInstance<VisitorSettings>();
        }

        /// <summary>
        /// Addressables로 관리하는 TMP 폰트를 MaterialReferenceManager 캐시에 미리 등록한다.
        /// </summary>
        private static void RegisterTmpFonts(Microsoft.Extensions.Logging.ILogger logger)
        {
            // TMP의 <font="..."> 태그는 이 캐시를 먼저 조회하고, 없으면 Resources에서만 폰트를 찾는다.
            // 등록해 두지 않으면 태그가 해석되지 않고 문자열 그대로 화면에 출력된다.
            // 첫 씬이 그려지기 전에 끝나야 하므로 동기 로드한다.
            try
            {
                IList<TMP_FontAsset> fonts = Addressables
                    .LoadAssetsAsync<TMP_FontAsset>(Constants.ResourcePaths.TmpFontLabel, null)
                    .WaitForCompletion();

                if (fonts is null)
                {
                    if (logger != null) logger.ZLogWarning($"[GameLifetimeScope] '{Constants.ResourcePaths.TmpFontLabel}' 라벨의 TMP 폰트를 찾지 못해 <font> 태그가 해석되지 않습니다.");
                    return;
                }

                foreach (TMP_FontAsset font in fonts)
                    if (font) MaterialReferenceManager.AddFontAsset(font);
            }
            catch (Exception ex)
            {
                // 폰트 등록 실패는 치명적이지 않다 — 태그가 해석되지 않을 뿐이므로 부팅은 계속 진행
                if (logger != null) logger.ZLogWarning($"[GameLifetimeScope] TMP 폰트 등록 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 씬 로드 이벤트를 구독하고, 이미 로드된 최초 씬에도 주입을 적용한다.
        /// </summary>
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

        /// <summary>
        /// 씬 로드 이벤트 구독을 해제한다.
        /// </summary>
        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        /// <summary>
        /// 새로 로드된 씬의 오브젝트에 [Inject]를 주입한다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // App(DontDestroyOnLoad) 하위는 컨테이너 빌드 시 이미 주입되므로 제외한다.
            if (scene == gameObject.scene) return;

            // 타이틀로 돌아온 경우(아웃트로 종료 버튼·비활동 타임아웃) 진행도 처리
            if (scene.name == Constants.Scenes.Title)
                HandleReturnToTitle();

            foreach (GameObject root in scene.GetRootGameObjects())
                Container.InjectGameObject(root);
        }

        /// <summary>
        /// 타이틀로 돌아오면 그 체험자의 체험이 끝나므로 운영 모드와 상관없이 진행도와 체험자 기록을 비운다.
        /// </summary>
        private void HandleReturnToTitle()
        {
            // 서버 미사용: 다음 체험자는 부팅 때처럼 레벨1부터 시작한다. 비활동 타임아웃으로 중간에 이탈한 경우도 같다.
            // 서버 사용: 진행도는 서버에 남아 있어, 다음에 QR을 찍으면 타이틀이 getUser로 그 체험자의 진행도를 다시 받는다.

            // 관리자 레벨 이동 표시(isAdminLevelJump)도 함께 비워진다 — 다음 체험자의 스토리 < 버튼·결과 다음 버튼이
            // 관리자 화면으로 가지 않는다
            Container.Resolve<GameSession>().ResetProgress();

            // QR로 확인한 체험자도 타이틀로 돌아오면 체험이 끝난다 — 다음 체험자는 QR로 다시 확인한다
            Container.Resolve<VisitorInfoProvider>().ClearServerVisitor();
        }
    }
}
