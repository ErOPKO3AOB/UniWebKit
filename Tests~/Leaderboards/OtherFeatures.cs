// Minimal doubles for the bridge's unrelated features. Leaderboard sources are real.
using System;

namespace UnityEngine { public static class Debug { public static void LogException(Exception exception) { } } }

namespace UniWebKit
{
    public interface IAdvProvider { }
    public interface IInAppProvider : IDisposable { }
    public interface IEnvirProvider { }
    public interface IPlayerProvider : IDisposable { }
    public interface ILocalizationProvider : IDisposable { }
    public class UnsupportedAdvProvider : IAdvProvider { }
    public class UnsupportedInAppProvider : IInAppProvider { public void Dispose() { } }
    public class UnityEnvirProvider : IEnvirProvider { }
    public class LocalPrefsPlayerProvider : IPlayerProvider { public void Dispose() { } }
    public class LocalLocalizationProvider : ILocalizationProvider { public void Dispose() { } }
}
