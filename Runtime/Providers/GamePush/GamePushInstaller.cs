#if UNIWEB_GAMEPUSH && UNIWEB_ARCHITECTURE_VCONTAINER
using VContainer;
using VContainer.Unity;

namespace UniWebKit
{
    public class GamePushInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GamePushAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();

            // Закомментировано до появления реализации
            // builder.Register<GamePushPlayerProvider>(Lifetime.Singleton).As<IPlayerProvider>();
        }
    }
}
#endif