using VContainer;
using VContainer.Unity;
using Wonjeong.App;

namespace DG.App
{
    public class GameLifetimeScope : RootLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();
        }
    }
}
