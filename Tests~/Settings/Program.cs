using System;
using UniWebKit;
using UnityEditor;

static class Program
{
    const string Legacy = "Assets/Resources/UniWebSettings.asset";
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static void Main()
    {
        var path = UniWebEditorUtils.SettingsAssetPath;
        AssetDatabase.Reset();
        var first = UniWebEditorUtils.GetOrCreateSettings();
        Check(path == "Assets/UniWebKit/Resources/UniWebSettings.asset", "single canonical path");
        Check(AssetDatabase.Created == 1 && AssetDatabase.Assets.Count == 1, "create once");
        Check(ReferenceEquals(first, UniWebEditorUtils.GetOrCreateSettings()), "idempotent lookup");
        UniWebEditorUtils.SelectOrCreateSettings();
        Check(AssetDatabase.Created == 1 && ReferenceEquals(Selection.activeObject, first), "Create menu selects existing asset");
        first.CurrentPlatform = UniWebSettings.TargetPlatform.VKGames;
        first.PlayerStorage.CloudKey = "custom";
        first.AutoInitializeStaticAPI = false;
        Check(UniWebEditorUtils.GetOrCreateSettings().PlayerStorage.CloudKey == "custom" &&
            first.CurrentPlatform == UniWebSettings.TargetPlatform.VKGames && !first.AutoInitializeStaticAPI, "existing settings preserved");

        AssetDatabase.Reset();
        var old = new UniWebSettings { CurrentPlatform = UniWebSettings.TargetPlatform.GamePush,
            Architecture = UniWebSettings.ArchitectureMode.VContainer };
        AssetDatabase.Assets.Add(Legacy, old); AssetDatabase.Guids.Add(Legacy, "original-guid");
        var moved = UniWebEditorUtils.GetOrCreateSettings();
        Check(ReferenceEquals(old, moved) && moved.Architecture == UniWebSettings.ArchitectureMode.VContainer, "legacy data retained");
        Check(AssetDatabase.Moved == 1 && AssetDatabase.Created == 0 && !AssetDatabase.Assets.ContainsKey(Legacy), "move instead of duplicate");
        Check(AssetDatabase.Guids[path] == "original-guid", "legacy GUID retained");
        UniWebEditorUtils.GetOrCreateSettings();
        Check(AssetDatabase.Moved == 1 && AssetDatabase.Assets.Count == 1, "legacy move is idempotent");

        AssetDatabase.Assets.Add(Legacy, new UniWebSettings());
        bool rejected = false;
        try { UniWebEditorUtils.GetOrCreateSettings(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && AssetDatabase.Assets.Count == 2, "conflicting configs not silently overwritten");

        AssetDatabase.Reset();
        AssetDatabase.Assets.Add(Legacy, old); AssetDatabase.Guids.Add(Legacy, "original-guid"); AssetDatabase.FailMove = true;
        rejected = false;
        try { UniWebEditorUtils.GetOrCreateSettings(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && AssetDatabase.Created == 0 && AssetDatabase.Assets.ContainsKey(Legacy), "failed move cannot create a second config");
        Console.WriteLine($"PASS: {checks} settings asset checks");
    }
}
