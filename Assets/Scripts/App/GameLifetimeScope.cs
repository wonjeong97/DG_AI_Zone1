using DG.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Wonjeong.App;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace DG.App
{
    public class GameLifetimeScope : RootLifetimeScope
    {
        private GameSession _session;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();

            // 앱 종료 버튼(GameCloser)·SystemCanvas — App/SystemCanvas 하위(스코프와 같은 씬)라 OnSceneLoaded 주입 대상이 아니고,
            // 아무도 Resolve하지 않으면 지연 등록만으로는 주입되지 않으므로 빌드 시점에 즉시 Resolve
            builder.RegisterComponentInHierarchy<GameCloser>();
            builder.RegisterComponentInHierarchy<SystemCanvas>();
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<GameCloser>();
                container.Resolve<SystemCanvas>();
            });

            // 전역 페이드 매니저 — App 하위에 생성되어 씬 전환 간 유지
            builder.RegisterComponentOnNewGameObject<FadeManager>(Lifetime.Singleton, "FadeManager")
                .UnderTransform(transform);
            builder.RegisterBuildCallback(container => container.Resolve<FadeManager>());

            // 게임 세션 데이터 — [Inject]로 주입 가능하도록 컨테이너에 등록
            // 앱을 껐다 켜면 항상 처음부터 시작하도록 부팅 시점에 진행도 초기화
            // VContainer Configure는 동기 실행이라 Addressables.WaitForCompletion(WebGL 미지원)을 쓸 수 없어 Resources.Load 유지
            _session = Resources.Load<GameSession>("Data/GameSession");
            _session.ResetProgress();
            builder.RegisterInstance(_session);
        }

        protected override void Awake()
        {
            base.Awake();
            SceneManager.sceneLoaded += OnSceneLoaded;
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

            // 타이틀로 돌아와 플로우를 다시 타는 경우도 부팅 시점과 동일하게 진행도 초기화
            if (scene.name == "0_Title")
                _session.ResetProgress();

            foreach (GameObject root in scene.GetRootGameObjects())
                Container.InjectGameObject(root);
        }
    }
}
