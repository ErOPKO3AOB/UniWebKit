using UnityEngine;

namespace UniWebKit
{
    public class UniWebSettings : ScriptableObject
    {
        public const string ResourceName = "UniWebSettings";
        public enum TargetPlatform { Custom, GamePush, PluginYG2, VKGames }

        public TargetPlatform CurrentPlatform = TargetPlatform.Custom;

        public PlayerStorageOptions PlayerStorage = new PlayerStorageOptions();

        public enum ArchitectureMode { Static, VContainer }

        [Tooltip("StaticBridge собирает всё в статический UniWebBridge. VContainer отключает автосборку для чистой DI-регистрации вручную.")]
        public ArchitectureMode Architecture = ArchitectureMode.Static;

        [Tooltip("Если включено, плагин сам соберет зависимости в статический класс UniWebAPI при старте игры.")]
        public bool AutoInitializeStaticAPI = true;
    }
}
