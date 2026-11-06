using UnityEngine;

namespace UniWebKit
{
    [CreateAssetMenu(fileName = "UniWebSettings", menuName = "UniWeb/Settings")]
    public class UniWebSettings : ScriptableObject
    {
        public enum TargetPlatform { Mock, GamePush, GameDistribution, PluginYG2 }

        public TargetPlatform CurrentPlatform = TargetPlatform.Mock;

        public enum ArchitectureMode { Static, VContainer }

        [Tooltip("StaticBridge собирает всё в статический UniWebBridge. VContainer отключает автосборку для чистой DI-регистрации.")]
        public ArchitectureMode Architecture = ArchitectureMode.Static;

        [Tooltip("Если включено, плагин сам соберет зависимости в статический класс UniWebAPI при старте игры.")]
        public bool AutoInitializeStaticAPI = true;
    }
}