#if UNIWEB_GAMEPUSH
using GamePush;

namespace UniWebKit
{
    public sealed class GamePushEnvirProvider : UnityEnvirProvider
    {
        public override bool IsReady => GP_Init.isReady && base.IsReady;

        public override EnvirDeviceType DeviceType
        {
            get
            {
                if (!GP_Init.isReady) return base.DeviceType;
                if (GP_Device.IsMobile()) return EnvirDeviceType.Mobile;
                if (GP_Device.IsDesktop()) return EnvirDeviceType.Desktop;
                return EnvirDeviceType.Unknown;
            }
        }
    }
}
#endif
