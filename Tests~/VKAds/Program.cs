using System;
using System.Reflection;
using UniWebKit;

namespace UnityEngine
{
    public static class AudioListener { public static bool pause; }
    public static class Debug { public static int Exceptions; public static void LogException(Exception error) => Exceptions++; }
}
namespace AOT
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MonoPInvokeCallbackAttribute : Attribute { public MonoPInvokeCallbackAttribute(Type type) { } }
}

static class Program
{
    static int checks;
    static readonly Type Provider = typeof(VKGamesAdvProvider);
    static readonly FieldInfo Pending = Provider.GetField("_pending", BindingFlags.NonPublic | BindingFlags.Static);
    static readonly Type Request = Provider.GetNestedType("PendingAd", BindingFlags.NonPublic);
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static void Signal(string method, params object[] args) => Provider.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    static void Begin(VKGamesAdvProvider owner, Action reward, Action<bool> closed, Action<string> error, Action opened = null)
    {
        var request = Activator.CreateInstance(Request);
        foreach (var item in new (string, object)[] { ("Owner", owner), ("Reward", reward), ("Closed", closed), ("Error", error), ("Opened", opened) })
            Request.GetField(item.Item1, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(request, item.Item2);
        Pending.SetValue(null, request);
    }
    static void Main()
    {
        var ads = new VKGamesAdvProvider();
        Check(!ads.IsSupported && !ads.IsInterstitialReady && !ads.IsRewardedReady, "Editor does not call browser imports");
        int reward = 0, closed = 0, errors = 0;
        ads.ShowRewarded(() => reward++, onClosed: success => { Check(!success, "Editor closes unsuccessfully"); closed++; }, onError: _ => errors++);
        Check(reward == 0 && closed == 1 && errors == 1, "unsupported does not reward");

        closed = errors = 0;
        bool? success = null;
        UnityEngine.AudioListener.pause = true;
        Begin(ads, () => reward++, value => { closed++; success = value; }, _ => errors++);
        Signal("OnAdOpened"); Signal("OnAdResult", 1);
        Check(reward == 1 && closed == 1 && errors == 0 && success == true, "reward and close once");
        Check(UnityEngine.AudioListener.pause, "preserve previously paused audio");
        Signal("OnAdResult", 1);
        Check(reward == 1 && closed == 1, "late duplicate without pending operation ignored");

        UnityEngine.AudioListener.pause = false;
        Begin(ads, () => reward++, value => success = value, _ => errors++);
        Signal("OnAdOpened");
        Check(UnityEngine.AudioListener.pause, "mute on dispatch");
        Signal("OnAdResult", 0);
        Check(!UnityEngine.AudioListener.pause && reward == 1 && success == false && errors == 1, "incomplete ad never rewards and restores audio");

        Begin(ads, () => throw new Exception("game callback"), _ => closed++, _ => errors++);
        Signal("OnAdResult", 1);
        Check(closed == 2 && UnityEngine.Debug.Exceptions == 1, "reward exception cannot skip close");

        int disposedRewards = 0, disposedErrors = 0, disposedClosed = 0;
        Begin(ads, () => disposedRewards++, _ => disposedClosed++, _ => disposedErrors++);
        Signal("OnAdOpened");
        ads.Dispose(); ads.Dispose();
        Check(disposedErrors == 1 && disposedClosed == 1 && !UnityEngine.AudioListener.pause, "dispose is idempotent and restores audio");
        Check(Pending.GetValue(null) != null, "disposed ad holds global lane until result");
        var replacement = new VKGamesAdvProvider();
        Signal("OnAdResult", 1);
        Check(disposedRewards == 0 && disposedClosed == 1 && Pending.GetValue(null) == null, "disposed late success cannot reward another instance");
        Begin(replacement, null, _ => closed++, _ => errors++);
        Signal("OnAdError", 2);
        Check(Pending.GetValue(null) == null && errors == 2, "SDK error completes and releases lane");
        Console.WriteLine($"PASS: {checks} VK C# adapter checks");
    }
}
