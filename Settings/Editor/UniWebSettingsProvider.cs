using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UniWebKit
{
    public static class UniWebSettingsProvider
    {
        private const string SettingsFileName = "UniWebSettings.asset";
        private const string ResourcesFolderPath = "Assets/Resources";
        private const string SettingsAssetPath = ResourcesFolderPath + "/" + SettingsFileName;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/UniWeb API", SettingsScope.Project)
            {
                guiHandler = (searchContext) =>
                {
                    var settings = GetOrCreateSettings();
                    SerializedObject so = new SerializedObject(settings);

                    // Начинаем следить за изменениями в UI
                    EditorGUI.BeginChangeCheck();

                    // ... код из guiHandler ...
                    EditorGUILayout.PropertyField(so.FindProperty("CurrentPlatform"));
                    EditorGUILayout.PropertyField(so.FindProperty("Architecture"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        UpdateDefines(settings);
                    }

                    // Отрисовываем галочку ТОЛЬКО если выбрана статическая архитектура
                    if (settings.Architecture == UniWebSettings.ArchitectureMode.Static)
                    {
                        EditorGUILayout.PropertyField(so.FindProperty("AutoInitializeStaticAPI")); //[cite: 13]
                    }

                    so.ApplyModifiedProperties();
                }
            };
            return provider;
        }

        private static void UpdateDefines(UniWebSettings settings)
        {
            // Используем современный NamedBuildTarget вместо устаревшего BuildTargetGroup
            var buildTarget = NamedBuildTarget.WebGL;

            var definesString = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var defines = definesString.Split(';').ToList();

            // 1. Вычищаем старые дефайны плагина, чтобы не было конфликтов
            defines.RemoveAll(d => d.StartsWith("UNIWEB_"));

            // 2. Прокидываем новый дефайн в зависимости от выбора
            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.GamePush:
                    defines.Add("UNIWEB_GAMEPUSH");
                    break;
                case UniWebSettings.TargetPlatform.GameDistribution:
                    defines.Add("UNIWEB_GAMEDISTRIBUTION");
                    break;
                case UniWebSettings.TargetPlatform.PluginYG2:
                    defines.Add("UNIWEB_PLUGIN_YG2");
                    break;
                    // Для Mock ничего не добавляем, он работает по умолчанию
            }

            // Управляем дефайном архитектуры
            defines.RemoveAll(d => d.StartsWith("UNIWEB_ARCH_"));

            if (settings.Architecture == UniWebSettings.ArchitectureMode.VContainer)
            {
                defines.Add("UNIWEB_ARCHITECTURE_VCONTAINER");
            }
            else
            {
                defines.Add("UNIWEB_ARCHITECTURE_STATIC");
            }

            // 3. Сохраняем новые дефайны
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", defines));

            Debug.Log($"[UniWeb API] Дефайны обновлены для WebGL. Активная платформа: {settings.CurrentPlatform}");
        }

        private static UniWebSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UniWebSettings>(SettingsAssetPath);
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<UniWebSettings>();

            if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return settings;
        }
    }
}