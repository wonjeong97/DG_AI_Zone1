using DG.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Wonjeong.App;
using Wonjeong.UI;

namespace DG.App
{
    public class GameLifetimeScope : RootLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();

            // 전역 페이드 매니저 — App 하위에 생성되어 씬 전환 간 유지
            builder.RegisterComponentOnNewGameObject<FadeManager>(Lifetime.Singleton, "FadeManager")
                .UnderTransform(transform);
            builder.RegisterBuildCallback(container => container.Resolve<FadeManager>());

            // 게임 세션 데이터 — [Inject]로 주입 가능하도록 컨테이너에 등록
            // 앱을 껐다 켜면 항상 처음부터 시작하도록 부팅 시점에 진행도 초기화
            GameSession session = Resources.Load<GameSession>("Data/GameSession");
            session.ResetProgress();
            builder.RegisterInstance(session);
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

            foreach (GameObject root in scene.GetRootGameObjects())
                Container.InjectGameObject(root);
        }
    }
}
