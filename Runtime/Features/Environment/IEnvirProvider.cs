namespace UniWebKit
{
    public interface IEnvirProvider : IUniWebFeature
    {
        bool IsReady { get; }
        EnvirDeviceType DeviceType { get; }
        bool IsMobile { get; }
        bool IsDesktop { get; }
    }
}
