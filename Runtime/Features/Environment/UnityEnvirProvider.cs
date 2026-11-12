using UnityEngine;

namespace UniWebKit
{
    public class UnityEnvirProvider : EnvirProviderBase
    {
        public override EnvirDeviceType DeviceType
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // The WebGL build target does not identify the device hosting its browser.
                // Wait for an SDK provider instead of reporting every browser as a desktop.
                return EnvirDeviceType.Unknown;
#else
                switch (SystemInfo.deviceType)
                {
                    case UnityEngine.DeviceType.Handheld: return EnvirDeviceType.Mobile;
                    case UnityEngine.DeviceType.Desktop: return EnvirDeviceType.Desktop;
                    default: return EnvirDeviceType.Unknown;
                }
#endif
            }
        }
    }
}
