using _.Scripts.Entry_Point;
using _.Scripts.Services;
using VContainer;
using VContainer.Unity;

namespace _.Scripts.Lifetime_Scope
{
    public class GameplayInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var gameplayService = new GameplayService();

            builder.RegisterInstance<GameplayService>(gameplayService).AsSelf();
            
            builder.RegisterEntryPoint<GameplayEntryPoint>();
        }
    }
}