#if UNIWEB_PLUGIN_YG2
using VContainer;
using VContainer.Unity;

namespace UniWebKit
{
    public class PluginYG2Installer : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<PluginYG2AdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
        }
    }
}
#endif