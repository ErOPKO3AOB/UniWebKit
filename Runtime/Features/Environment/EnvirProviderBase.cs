namespace UniWebKit
{
    public abstract class EnvirProviderBase : IEnvirProvider
    {
        public virtual bool IsSupported => true;
        public virtual bool IsReady => DeviceType != EnvirDeviceType.Unknown;
        public virtual EnvirDeviceType DeviceType => EnvirDeviceType.Unknown;
        public bool IsMobile => DeviceType == EnvirDeviceType.Mobile;
        public bool IsDesktop => DeviceType == EnvirDeviceType.Desktop;
    }
}
