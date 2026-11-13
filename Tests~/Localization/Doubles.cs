// Managed doubles for behavioral checks; not a replacement for Unity/WebGL integration checks.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum SystemLanguage { Afrikaans, Arabic, Basque, Belarusian, Bulgarian, Catalan,
        Chinese, ChineseSimplified, ChineseTraditional, Czech, Danish, Dutch, Estonian,
        Faroese, Finnish, French, German, Greek, Hebrew, Hungarian, Icelandic, Indonesian,
        Italian, Japanese, Korean, Latvian, Lithuanian, Norwegian, Polish, Portuguese,
        Romanian, Russian, SerboCroatian, Slovak, Slovenian, Spanish, Swedish, Thai,
        Turkish, Ukrainian, Vietnamese, Unknown }
    public static class Application { public static SystemLanguage systemLanguage = SystemLanguage.Russian; }
    public class GameObject
    {
        internal MonoBehaviour component;
        public GameObject(string name) { }
        public T AddComponent<T>() where T : MonoBehaviour, new()
        { var value = new T { gameObject = this }; component = value; return value; }
    }
    public class MonoBehaviour
    {
        private static readonly Dictionary<MonoBehaviour, IEnumerator> Running = new();
        public GameObject gameObject;
        public static void DontDestroyOnLoad(GameObject obj) { }
        public static void Destroy(GameObject obj) => obj.component.StopAllCoroutines();
        public void StartCoroutine(IEnumerator routine) { Running[this] = routine; routine.MoveNext(); }
        public void StopAllCoroutines() => Running.Remove(this);
        public static void Tick()
        { foreach (var pair in new Dictionary<MonoBehaviour, IEnumerator>(Running)) pair.Value.MoveNext(); }
        public static int Pending => Running.Count;
    }
}
namespace GamePush
{
    public enum Language { English, Russian, Turkish, French, Italian, German, Spanish,
        Chineese, Portuguese, Korean, Japanese, Arab, Hindi, Indonesian }
    public static class GP_Init { public static bool isReady; }
    public static class GP_Language
    {
        public static event Action<Language> OnChangeLanguage;
        public static Language current = Language.Turkish;
        public static Language Current() => current;
        public static void Change(Language language) { current = language; OnChangeLanguage?.Invoke(language); }
    }
}
namespace YG
{
    public static class YG2
    {
        public static bool isSDKEnabled;
#if Localization_yg
        public static string lang = "en";
        public static Action<string> onSwitchLang;
        public static void SwitchLanguage(string code) { lang = code; onSwitchLang?.Invoke(code); }
#endif
    }
}
namespace UniWebKit
{
    public interface IAdvProvider { }
    public interface IInAppProvider : IDisposable { }
    public interface IEnvirProvider { }
    public interface IPlayerProvider : IDisposable { }
    public class UnsupportedAdvProvider : IAdvProvider { }
    public class UnsupportedInAppProvider : IInAppProvider { public void Dispose() { } }
    public class UnityEnvirProvider : IEnvirProvider { }
    public class LocalPrefsPlayerProvider : IPlayerProvider { public void Dispose() { } }
}
