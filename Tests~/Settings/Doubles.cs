using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class ScriptableObject { public static T CreateInstance<T>() where T : new() => new T(); }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public static class Debug { public static void Log(string value) { } }
}
namespace UniWebKit { public sealed class PlayerStorageOptions { public string CloudKey = "default"; } }
namespace UnityEditor.Build { public struct NamedBuildTarget { public static NamedBuildTarget WebGL => default; } }
namespace UnityEditor
{
    public class MenuItem : Attribute { public MenuItem(string path) { } }
    public static class Selection { public static object activeObject; }
    public static class EditorGUIUtility { public static void PingObject(object obj) { } }
    public static class PlayerSettings
    {
        public static string Symbols = "";
        public static string GetScriptingDefineSymbols(Build.NamedBuildTarget target) => Symbols;
        public static void SetScriptingDefineSymbols(Build.NamedBuildTarget target, string value) => Symbols = value;
    }
    public static class AssetDatabase
    {
        public static readonly Dictionary<string, UniWebKit.UniWebSettings> Assets = new();
        public static readonly Dictionary<string, string> Guids = new();
        public static readonly HashSet<string> Folders = new();
        public static int Created, Moved;
        public static bool FailMove;
        public static void Reset() { Assets.Clear(); Guids.Clear(); Folders.Clear(); Folders.Add("Assets"); Created = Moved = 0; FailMove = false; }
        public static T LoadAssetAtPath<T>(string path) where T : class => Assets.TryGetValue(path, out var asset) ? asset as T : null;
        public static bool IsValidFolder(string path) => Folders.Contains(path);
        public static string CreateFolder(string parent, string name)
        {
            if (!Folders.Contains(parent)) throw new Exception("Missing parent directory");
            Folders.Add(parent + "/" + name); return "folder-guid";
        }
        public static void CreateAsset(UniWebKit.UniWebSettings asset, string path)
        { Assets.Add(path, asset); Guids.Add(path, "new-guid"); Created++; }
        public static string MoveAsset(string source, string target)
        {
            if (FailMove) return "Asset move failed";
            Assets.Add(target, Assets[source]); Assets.Remove(source);
            Guids.Add(target, Guids[source]); Guids.Remove(source); Moved++; return "";
        }
        public static void SaveAssets() { }
        public static void Refresh() { }
    }
}
