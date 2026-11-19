using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Platform = UniWebKit.UniWebSettings.TargetPlatform;

namespace UniWebKit
{
    internal sealed class UniWebSdk
    {
        internal sealed class Artifact
        {
            internal readonly string Url;
            internal readonly string Sha256;
            internal Artifact(string url, string sha256) { Url = url; Sha256 = sha256; }
        }

        internal const string VkScript = "vk-bridge.js";
        internal static readonly UniWebSdk[] All =
        {
            new UniWebSdk(Platform.VKGames, "@vkontakte/vk-bridge", "3.0.2", "VKGames",
                "Assets/UniWebKit/VKBridge", VkScript,
                new Artifact("https://unpkg.com/@vkontakte/vk-bridge@3.0.2/dist/browser.min.js",
                    "827ba6f457cd9e73519b58b2c66564caf138645202f1bba37dca0b567f601bb4")),
            new UniWebSdk(Platform.GamePush, "GamePush", "2.02.0", "GamePush",
                "Assets/Plugins/GamePush", "Runtime/GamePush.Runtime.asmdef",
                new Artifact("https://github.com/GamePushService/GamePush-Unity-plugin/releases/download/v2.02.0/GamePush_v2.02.0.unitypackage",
                    "71b4b0f7531eea404d0284dcdf2d026c4402004e668025c729be4facdfabe4cc")),
            new UniWebSdk(Platform.PluginYG2, "YG2", "2.01", "YandexGames",
                "Assets/PluginYourGames", "YG2.asmdef",
                new Artifact("https://github.com/JustPlay-Max/Unity-PluginYG-2/releases/download/2.01/PluginYG2-01.unitypackage",
                    "ad41a2ad739f824a927d5d93de2f3c92e3b66bd2f65c7be86bd0f24d0ae4cf3e"))
        };

        internal readonly Platform Platform;
        internal readonly string TechnicalName, Version, Template, Root, EntryPoint;
        internal readonly Artifact Download;

        private UniWebSdk(Platform platform, string name, string version, string template,
            string root, string entryPoint, Artifact download)
        {
            Platform = platform;
            TechnicalName = name;
            Version = version;
            Template = template;
            Root = root;
            EntryPoint = entryPoint;
            Download = download;
        }

        internal string TemplatePath => "Assets/WebGLTemplates/" + Template;
        internal string WebGLTemplate => Platform == Platform.VKGames && !File.Exists(TemplatePath + "/index.html")
            ? "APPLICATION:Default" : "PROJECT:" + Template;
        internal string Receipt => TechnicalName + " " + Version + "\n" + Download.Sha256;
        internal string ReceiptPath => Root + "/uniwebkit-sdk.txt";
        internal bool IsInstalled => Directory.Exists(Root);
        internal bool IsPinned
        {
            get
            {
                string entry = Root + "/" + EntryPoint;
                if (!File.Exists(ReceiptPath) || File.ReadAllText(ReceiptPath) != Receipt || !File.Exists(entry))
                    return false;
                if (Platform == Platform.VKGames)
                    return UniWebSdkArchive.Hash(File.ReadAllBytes(entry)) == Download.Sha256;
                if (Platform == Platform.PluginYG2)
                    return File.Exists(Root + "/Version.txt") && File.ReadAllText(Root + "/Version.txt").Trim().TrimStart('v') == Version;
                return File.Exists(Root + "/Data/PluginData.cs") &&
                    File.ReadAllText(Root + "/Data/PluginData.cs").Contains("SDK_VERSION = \"" + Version + "\"");
            }
        }

        internal static UniWebSdk Find(Platform platform) => All.FirstOrDefault(sdk => sdk.Platform == platform);

        internal static string FindExternalSdk()
        {
            foreach (var sdk in All.Where(item => item.Platform != Platform.VKGames))
            {
                string path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(sdk.TechnicalName);
                if (string.IsNullOrEmpty(path) || path.StartsWith(sdk.Root + "/", StringComparison.Ordinal)) continue;
                return (PackageInfo.FindForAssetPath(path)?.name ?? sdk.TechnicalName) + " (" + path + ")";
            }

            foreach (var asset in AssetDatabase.GetAllAssetPaths())
            {
                string file = Path.GetFileName(asset);
                var sdk = All.FirstOrDefault(item => item.Platform != Platform.VKGames &&
                    (file == item.TechnicalName + ".asmdef" || file == Path.GetFileName(item.EntryPoint) ||
                     (item.Platform == Platform.PluginYG2 && file == "YG2.cs")));
                if (sdk != null && !asset.StartsWith(sdk.Root + "/", StringComparison.Ordinal))
                    return sdk.TechnicalName + " (" + asset + ")";
            }
            return null;
        }

    }
}
