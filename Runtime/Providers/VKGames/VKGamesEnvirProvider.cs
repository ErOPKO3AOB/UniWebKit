#if UNIWEB_VK_GAMES
namespace UniWebKit
{
    public sealed class VKGamesEnvirProvider : UnityEnvirProvider
    {
        public VKGamesEnvirProvider()
        {
            if (VKGamesRuntime.IsBrowser) _ = VKGamesRuntime.Instance;
        }

        public override EnvirDeviceType DeviceType => VKGamesRuntime.IsBrowser
            ? VKGamesRuntime.DeviceType : base.DeviceType;
    }
}
#endif
