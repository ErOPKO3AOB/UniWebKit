using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UniWebKit
{
    public static class UniWebEditorUtils
    {
        private const string SettingsFileName = "UniWebSettings.asset";

        private const string ResourcesFolderPath = "Assets/UniWebKit/Resources";
        private const string SettingsAssetPath = ResourcesFolderPath + "/" + SettingsFileName;

        public static UniWebSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UniWebSettings>(SettingsAssetPath);
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<UniWebSettings>();

            if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                Directory.CreateDirectory(ResourcesFolderPath);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return settings;
        }

        public static void UpdateDefines(UniWebSettings settings)
        {
            var buildTarget = NamedBuildTarget.WebGL;
            var definesString = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var defines = definesString.Split(';').ToList();

            defines.RemoveAll(d => d.StartsWith("UNIWEB_"));

            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.GamePush:
                    defines.Add("UNIWEB_GAMEPUSH");
                    break;
                case UniWebSettings.TargetPlatform.PluginYG2:
                    defines.Add("UNIWEB_PLUGIN_YG2");
                    break;
                case UniWebSettings.TargetPlatform.VKGames:
                    defines.Add("UNIWEB_VK_GAMES");
                    break;
            }

            defines.RemoveAll(d => d.StartsWith("UNIWEB_ARCH_"));

            if (settings.Architecture == UniWebSettings.ArchitectureMode.VContainer)
            {
                defines.Add("UNIWEB_ARCHITECTURE_VCONTAINER");
            }
            else
            {
                defines.Add("UNIWEB_ARCHITECTURE_STATIC");
            }

            PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", defines));
            Debug.Log($"[UniWeb API] Дефайны обновлены для WebGL. Активная платформа: {settings.CurrentPlatform}");
        }
    }
}