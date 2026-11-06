using UnityEditor;
using UnityEngine;

namespace UniWebKit
{
    public class UniWebSettingsWindow : EditorWindow
    {
        private UniWebSettings _settings;
        private SerializedObject _serializedSettings;

        // Добавляем пункт в верхнее меню Unity
        [MenuItem("Window/UniWeb API Settings")]
        public static void ShowWindow()
        {
            // Открываем окно с заданным заголовком
            var window = GetWindow<UniWebSettingsWindow>("UniWeb Settings");
            window.minSize = new Vector2(300, 150);
            window.Show();
        }

        private void OnEnable()
        {
            // Загружаем конфиг при открытии окна
            _settings = UniWebEditorUtils.GetOrCreateSettings();
            _serializedSettings = new SerializedObject(_settings);
        }

        private void OnGUI()
        {
            if (_serializedSettings == null) return;

            // Отрисовываем заголовок
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unified Web Games Kit Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _serializedSettings.Update();

            // Следим за изменениями, точно так же, как в SettingsProvider
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("CurrentPlatform"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("Architecture"));

            if (EditorGUI.EndChangeCheck())
            {
                _serializedSettings.ApplyModifiedProperties();
                UniWebEditorUtils.UpdateDefines(_settings);
            }

            // Отрисовываем галочку только для статической архитектуры
            if (_settings.Architecture == UniWebSettings.ArchitectureMode.Static)
            {
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("AutoInitializeStaticAPI"));
            }

            _serializedSettings.ApplyModifiedProperties();

            // Кнопка для ручного пинга (выделения) файла настроек в Project окне
            EditorGUILayout.Space(20);
            if (GUILayout.Button("Select Settings Asset"))
            {
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }
        }
    }
}