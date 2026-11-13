using UnityEditor;

namespace UniWebKit
{
    public static class UniWebSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/UniWeb API", SettingsScope.Project)
            {
                guiHandler = (searchContext) =>
                {
                    var settings = UniWebEditorUtils.GetOrCreateSettings();
                    SerializedObject so = new SerializedObject(settings);

                    // Начинаем следить за изменениями в UI
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(so.FindProperty("CurrentPlatform"));
                    EditorGUILayout.PropertyField(so.FindProperty("Architecture"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        UniWebEditorUtils.UpdateDefines(settings);
                    }

                    // Отрисовываем галочку ТОЛЬКО если выбрана статическая архитектура
                    if (settings.Architecture == UniWebSettings.ArchitectureMode.Static)
                    {
                        EditorGUILayout.PropertyField(so.FindProperty("AutoInitializeStaticAPI"));
                    }

                    EditorGUILayout.PropertyField(so.FindProperty("PlayerStorage"), true);
                    so.ApplyModifiedProperties();
                }
            };
            return provider;
        }
    }
}
