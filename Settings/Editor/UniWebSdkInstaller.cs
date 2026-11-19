using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;
using Platform = UniWebKit.UniWebSettings.TargetPlatform;

namespace UniWebKit
{
    internal static class UniWebSdkInstaller
    {
        private const string BackupRoot = "Library/UniWebKit/SDKBackups";
        internal static bool IsSwitching { get; private set; }
        internal static string Error { get; private set; }

        internal static void DrawPicker(UniWebSettings settings)
        {
            var sdk = UniWebSdk.Find(settings.CurrentPlatform);
            using (new EditorGUI.DisabledScope(IsSwitching || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var selected = (Platform)EditorGUILayout.EnumPopup("SDK", settings.CurrentPlatform);
                if (selected != settings.CurrentPlatform)
                    Switch(settings, selected);
                else if (sdk != null)
                {
                    bool configured = sdk.IsPinned && PlayerSettings.WebGL.template == sdk.WebGLTemplate;
                    EditorGUILayout.LabelField(sdk.TechnicalName, sdk.Version + (configured ? " (installed)" : " (install required)"));
                    if ((!configured || UniWebSdk.All.Any(other => other != sdk && other.IsInstalled)) &&
                        GUILayout.Button("Install pinned SDK"))
                        Switch(settings, settings.CurrentPlatform);
                }
            }
            if (IsSwitching)
                EditorGUILayout.HelpBox("Downloading and installing the selected SDK…", MessageType.Info);
            else if (Error != null)
                EditorGUILayout.HelpBox(Error, MessageType.Error);
        }

        private static async void Switch(UniWebSettings settings, Platform target)
        {
            IsSwitching = true;
            Error = null;
            var sdk = UniWebSdk.Find(target);
            string stage = "Temp/UniWebKit/" + Guid.NewGuid().ToString("N");
            try
            {
                var external = UniWebSdk.FindExternalSdk();
                if (external != null)
                    throw new InvalidOperationException("An SDK is installed outside the managed folder: " + external);
                if (sdk != null && (!sdk.IsPinned || sdk.Platform != Platform.VKGames && !File.Exists(sdk.TemplatePath + "/index.html")))
                    await Prepare(sdk, stage);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before switching the SDK.");
                Apply(settings, target, sdk, stage);
                Debug.Log("[UniWebKit] Active SDK: " + (sdk == null ? "Custom" : sdk.TechnicalName + " " + sdk.Version));
            }
            catch (Exception error)
            {
                Error = error.Message;
                Debug.LogError("[UniWebKit] " + Error);
            }
            finally
            {
                IsSwitching = false;
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }

        internal static void ExtractArtifact(UniWebSdk sdk, byte[] data, string staging)
        {
            if (sdk.Platform == Platform.VKGames)
            {
                string root = Path.Combine(staging, sdk.Root);
                Directory.CreateDirectory(root);
                File.WriteAllBytes(Path.Combine(root, UniWebSdk.VkScript), data);
            }
            else
                UniWebSdkArchive.Extract(data, staging, path =>
                    (path == sdk.Root || path.StartsWith(sdk.Root + "/", StringComparison.Ordinal) ||
                     path == sdk.TemplatePath || path.StartsWith(sdk.TemplatePath + "/", StringComparison.Ordinal)) &&
                    !path.EndsWith("/Example", StringComparison.Ordinal) && !path.Contains("/Example/"));
        }

        private static async Task Prepare(UniWebSdk sdk, string stage)
        {
            var artifact = sdk.Download;
            using (var request = UnityWebRequest.Get(artifact.Url))
            {
                request.timeout = 30;
                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Delay(50);
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException("Could not download " + artifact.Url + ". " + request.error);
                var data = request.downloadHandler.data;
                if (UniWebSdkArchive.Hash(data) != artifact.Sha256)
                    throw new InvalidDataException("SHA-256 mismatch for " + artifact.Url);
                ExtractArtifact(sdk, data, stage);
            }
            CompleteStaging(sdk, stage);
        }

        internal static void CompleteStaging(UniWebSdk sdk, string stage)
        {
            if (sdk.Platform == Platform.PluginYG2)
            {
                CreateYgAssemblies(Path.Combine(stage, sdk.Root));
                CreateYgSettings(Path.Combine(stage, sdk.Root));
            }
            if (!File.Exists(Path.Combine(stage, sdk.Root, sdk.EntryPoint)))
                throw new InvalidDataException("The SDK entry point is missing: " + sdk.EntryPoint);
            File.WriteAllText(Path.Combine(stage, sdk.ReceiptPath), sdk.Receipt);
        }

        private static void CreateYgAssemblies(string root)
        {
            // PluginYG2 ships loose scripts. Give the adapter a stable assembly to reference.
            File.WriteAllText(Path.Combine(root, "YG2.asmdef"),
                "{\"name\":\"YG2\",\"references\":[\"UnityEngine.UI\",\"Unity.TextMeshPro\"]}");
            string editor = Path.Combine(root, "Editor");
            Directory.CreateDirectory(editor);
            File.WriteAllText(Path.Combine(editor, "YG2.Editor.asmdef"),
                "{\"name\":\"YG2.Editor\",\"references\":[\"YG2\",\"UnityEngine.UI\",\"Unity.TextMeshPro\"],\"includePlatforms\":[\"Editor\"]}");
            EnsureYgEditorReferences(root);
        }

        internal static bool EnsureYgEditorReferences(string root)
        {
            bool changed = false;
            foreach (string folder in Directory.GetDirectories(root, "Editor", SearchOption.AllDirectories))
            {
                if (Directory.GetFiles(folder, "*.asmdef").Length != 0 || Directory.GetFiles(folder, "*.asmref").Length != 0)
                    continue;
                File.WriteAllText(Path.Combine(folder, "YG2.Editor.asmref"), "{\"reference\":\"YG2.Editor\"}");
                changed = true;
            }
            return changed;
        }

        private static void CreateYgSettings(string root)
        {
            string platform = Path.Combine(root, "Platforms/YandexGames/YandexGames");
            string platformGuid = ReadGuid(platform + ".txt.meta");
            File.Move(platform + ".txt", platform + ".asset");
            File.Delete(platform + ".txt.meta");
            File.WriteAllText(platform + ".asset.meta",
                "fileFormatVersion: 2\nguid: " + platformGuid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n");

            string scriptGuid = ReadGuid(Path.Combine(root, "Scripts/InfoYG/InfoYG.cs.meta"));
            string resources = Path.Combine(root, "Resources");
            Directory.CreateDirectory(resources);
            File.WriteAllText(Path.Combine(resources, "SettingsYG2.asset"),
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                "  m_Enabled: 1\n  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}\n" +
                "  m_Name: SettingsYG2\n  Basic:\n    platform: {fileID: 11400000, guid: " + platformGuid + ", type: 2}\n" +
                "    autoApplySettings: 0\n    archivingBuild: 0\n    autoDefineSymbols: 1\n");
        }

        private static string ReadGuid(string path) => File.ReadLines(path).First(line => line.StartsWith("guid: ")).Substring(6).Trim();

        private static void Apply(UniWebSettings settings, Platform target, UniWebSdk sdk, string stage)
        {
            string template = sdk == null ? null : sdk.TemplatePath;
            bool needsTemplate = template != null && sdk.Platform != Platform.VKGames && !File.Exists(template + "/index.html");
            if (needsTemplate && !File.Exists(Path.Combine(stage, template, "index.html")))
                throw new InvalidOperationException("Missing WebGL template: " + template);

            bool sdkWasPinned = sdk != null && sdk.IsPinned;
            var previousPlatform = settings.CurrentPlatform;
            var previousDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            var previousTemplate = PlayerSettings.WebGL.template;
            string backup = Path.Combine(BackupRoot, DateTime.UtcNow.ToString("yyyyMMddHHmmssffff") + "-" + Guid.NewGuid().ToString("N"));
            var moves = new List<(string Source, string Destination, bool Directory)>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var other in UniWebSdk.All)
                {
                    if (other == sdk && sdkWasPinned) continue;
                    MoveAsset(other.Root, Path.Combine(backup, other.Root), moves);
                }

                if (sdk != null && !sdkWasPinned)
                {
                    RestoreVendorSettings(sdk, stage);
                    MoveAsset(Path.Combine(stage, sdk.Root), sdk.Root, moves);
                }

                if (needsTemplate)
                    InstallMissingTemplateFiles(Path.Combine(stage, template), template, moves);

                settings.CurrentPlatform = target;
                EditorUtility.SetDirty(settings);
                if (sdk != null) PlayerSettings.WebGL.template = sdk.WebGLTemplate;
                else if (UniWebSdk.All.Any(other => previousTemplate == "PROJECT:" + other.Template))
                    PlayerSettings.WebGL.template = "APPLICATION:Default";
                UniWebEditorUtils.UpdateDefines(settings, previousPlatform != target || !sdkWasPinned);
                AssetDatabase.SaveAssets();
            }
            catch
            {
                for (int i = moves.Count - 1; i >= 0; i--)
                {
                    var move = moves[i];
                    if (move.Directory) Directory.Move(move.Destination, move.Source);
                    else File.Move(move.Destination, move.Source);
                }
                settings.CurrentPlatform = previousPlatform;
                EditorUtility.SetDirty(settings);
                PlayerSettings.WebGL.template = previousTemplate;
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, previousDefines);
                AssetDatabase.SaveAssets();
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static void MoveAsset(string source, string destination, List<(string Source, string Destination, bool Directory)> moves)
        {
            if (Directory.Exists(source)) MovePath(source, destination, moves, isDirectory: true);
            if (File.Exists(source + ".meta")) MovePath(source + ".meta", destination + ".meta", moves);
        }

        private static void MovePath(string source, string destination,
            List<(string Source, string Destination, bool Directory)> moves, bool isDirectory = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (isDirectory) Directory.Move(source, destination);
            else File.Move(source, destination);
            moves.Add((source, destination, isDirectory));
        }

        private static void InstallMissingTemplateFiles(string source, string destination,
            List<(string Source, string Destination, bool Directory)> moves)
        {
            if (!Directory.Exists(destination))
                MovePath(source, destination, moves, isDirectory: true);
            else
            {
                foreach (string folder in Directory.GetDirectories(source))
                    InstallMissingTemplateFiles(folder, Path.Combine(destination, Path.GetFileName(folder)), moves);

                foreach (string file in Directory.GetFiles(source))
                {
                    string path = Path.Combine(destination, Path.GetFileName(file));
                    if (!File.Exists(path)) MovePath(file, path, moves);
                }
            }

            if (File.Exists(source + ".meta") && !File.Exists(destination + ".meta"))
                MovePath(source + ".meta", destination + ".meta", moves);
        }

        private static void RestoreVendorSettings(UniWebSdk sdk, string stage)
        {
            string[] settings = sdk.Platform == Platform.GamePush
                ? new[] { "Data/GP_SettingsWrap.asset", "Resources/GP_PlatformSettings.asset" }
                : sdk.Platform == Platform.PluginYG2
                    ? new[] { "Resources/SettingsYG2.asset", "Editor/PluginPrefs.json",
                        "Platforms/YandexGames/YandexGames.asset" }
                    : Array.Empty<string>();
            if (settings.Length == 0 || !Directory.Exists(BackupRoot)) return;
            foreach (string local in settings)
            {
                string path = sdk.Root + "/" + local;
                string source = Directory.GetDirectories(BackupRoot).OrderByDescending(file => file, StringComparer.Ordinal)
                    .Select(backup => Path.Combine(backup, path)).FirstOrDefault(File.Exists);
                if (source == null) continue;
                string destination = Path.Combine(stage, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
                if (File.Exists(source + ".meta")) File.Copy(source + ".meta", destination + ".meta", true);
            }
        }
    }

    internal sealed class UniWebYgEditorReferences : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var sdk = UniWebSdk.Find(Platform.PluginYG2);
            if (!sdk.IsPinned || !imported.Any(path => path.StartsWith(sdk.Root + "/", StringComparison.Ordinal)))
                return;
            if (UniWebSdkInstaller.EnsureYgEditorReferences(sdk.Root))
                EditorApplication.delayCall += () => AssetDatabase.Refresh();
        }
    }
}
