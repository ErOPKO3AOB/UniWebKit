using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UniWebKit
{
    internal sealed class UniWebSdkBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            var settings = UniWebEditorUtils.GetOrCreateSettings();
            var selected = UniWebSdk.Find(settings.CurrentPlatform);

            if (UniWebSdkInstaller.IsSwitching)
                throw new BuildFailedException("[UniWebKit] Wait for the SDK installation to finish.");
            var external = UniWebSdk.FindExternalSdk();
            if (external != null)
                throw new BuildFailedException("[UniWebKit] SDK installed outside the managed folder: " + external);
            var extra = UniWebSdk.All.FirstOrDefault(sdk => sdk != selected && sdk.IsInstalled);
            if (extra != null)
                throw new BuildFailedException("[UniWebKit] Extra SDK installed: " + extra.TechnicalName + ". Apply the SDK selection in Project Settings.");
            string output = report.summary.outputPath;
            string outputStamp = Path.Combine(output, "TemplateData", "uniwebkit-sdk.txt");
            if (File.Exists(outputStamp) && File.ReadAllText(outputStamp) != (selected?.TechnicalName ?? "Custom"))
                throw new BuildFailedException("[UniWebKit] Choose a separate output folder for this SDK.");
            if (File.Exists(Path.Combine(output, "TemplateData", UniWebSdk.VkScript)) &&
                selected?.Platform != UniWebSettings.TargetPlatform.VKGames)
                throw new BuildFailedException("[UniWebKit] The output folder contains VK Bridge. Choose a separate output folder.");
            if (selected == null) return;
            if (!selected.IsPinned)
                throw new BuildFailedException("[UniWebKit] Install " + selected.TechnicalName + " " + selected.Version + " in Project Settings.");
            if (PlayerSettings.WebGL.template != selected.WebGLTemplate)
                throw new BuildFailedException("[UniWebKit] Select the " + selected.Template + " WebGL template or reapply the SDK selection.");
            var symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL).Split(';');
            string expected = selected.Platform == UniWebSettings.TargetPlatform.VKGames ? "UNIWEB_VK_GAMES" :
                selected.Platform == UniWebSettings.TargetPlatform.GamePush ? "UNIWEB_GAMEPUSH" : "UNIWEB_PLUGIN_YG2";
            if (!symbols.Contains(expected) || symbols.Count(s => s == "UNIWEB_VK_GAMES" || s == "UNIWEB_GAMEPUSH" || s == "UNIWEB_PLUGIN_YG2") != 1)
                throw new BuildFailedException("[UniWebKit] WebGL defines are inconsistent with " + selected.TechnicalName + ". Reapply the SDK selection.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            var settings = UniWebEditorUtils.GetOrCreateSettings();
            var selected = UniWebSdk.Find(settings.CurrentPlatform);
            string templateData = Path.Combine(report.summary.outputPath, "TemplateData");
            Directory.CreateDirectory(templateData);
            if (selected?.Platform == UniWebSettings.TargetPlatform.VKGames)
                File.Copy(Path.Combine(selected.Root, UniWebSdk.VkScript), Path.Combine(templateData, UniWebSdk.VkScript), true);
            File.WriteAllText(Path.Combine(templateData, "uniwebkit-sdk.txt"), selected?.TechnicalName ?? "Custom");
        }
    }
}
