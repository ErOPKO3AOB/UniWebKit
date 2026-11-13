using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UniWebKit
{
    public static class UniWebEditorUtils
    {
        public const string ResourcesFolderPath = "Assets/UniWebKit/Resources";
        public const string SettingsAssetPath = ResourcesFolderPath + "/" + UniWebSettings.ResourceName + ".asset";
        private const string LegacySettingsAssetPath = "Assets/Resources/UniWebSettings.asset";

        [MenuItem("Assets/Create/UniWeb/Settings")]
        public static void SelectOrCreateSettings()
        {
            var settings = GetOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        public static UniWebSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UniWebSettings>(SettingsAssetPath);
            var legacy = AssetDatabase.LoadAssetAtPath<UniWebSettings>(LegacySettingsAssetPath);
            if (settings != null && legacy != null)
                throw new InvalidOperationException($"Two UniWebSettings assets exist. Keep {SettingsAssetPath} and remove the obsolete {LegacySettingsAssetPath} after checking its settings and references.");
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/UniWebKit"))
                    AssetDatabase.CreateFolder("Assets", "UniWebKit");
                AssetDatabase.CreateFolder("Assets/UniWebKit", "Resources");
            }

            // Older installations used Assets/Resources. Move the asset without changing its GUID/data.
            if (legacy != null)
            {
                var error = AssetDatabase.MoveAsset(LegacySettingsAssetPath, SettingsAssetPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                AssetDatabase.SaveAssets();
                return legacy;
            }

            settings = ScriptableObject.CreateInstance<UniWebSettings>();
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
