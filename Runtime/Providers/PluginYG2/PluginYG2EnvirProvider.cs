#if UNIWEB_PLUGIN_YG2
#if EnvirData_yg
using YG;
#endif

namespace UniWebKit
{
    public sealed class PluginYG2EnvirProvider : UnityEnvirProvider
    {
#if EnvirData_yg
        public override bool IsReady => YG2.isSDKEnabled && YG2.envir != null && base.IsReady;

        public override EnvirDeviceType DeviceType
        {
            get
            {
                if (!YG2.isSDKEnabled || YG2.envir == null) return base.DeviceType;
                if (YG2.envir.isMobile || YG2.envir.isTablet) return EnvirDeviceType.Mobile;
                if (YG2.envir.isDesktop) return EnvirDeviceType.Desktop;
                return EnvirDeviceType.Unknown;
            }
        }
#endif
    }
}
#endif
