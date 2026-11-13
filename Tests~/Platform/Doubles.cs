using System;
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public GameObject gameObject;
        public static void DontDestroyOnLoad(GameObject obj) { }
    }
    public class GameObject
    {
        public string name;
        public GameObject(string value) { name = value; }
        public T AddComponent<T>() where T : MonoBehaviour, new() => new T { gameObject = this };
    }
    public static class Time { public static float realtimeSinceStartup; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public static class AudioListener { public static bool pause; }
    public static class Debug { public static int Exceptions; public static void LogException(Exception e) { Exceptions++; } }
    public static class JsonUtility { public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }); }
}
namespace UnityEngine.Scripting { public class PreserveAttribute : Attribute { } }
namespace AOT { public class MonoPInvokeCallbackAttribute : Attribute { public MonoPInvokeCallbackAttribute(Type type) { } } }
namespace YG
{
    public class Product
    {
        public string id, title, description, priceValue, price, priceCurrencyCode, imageURI, currencyImageURL;
        public bool consumed;
    }
    public static class YG2
    {
        public static bool isSDKEnabled;
        public static Product[] purchases;
        public static Action<string> onPurchaseSuccess, onPurchaseFailed;
        public static void BuyPayments(string id)
        {
            foreach (var product in purchases) if (product.id == id) product.consumed = true;
            onPurchaseSuccess?.Invoke(id);
        }
        public static void ConsumePurchaseByID(string id, bool notify) => BuyPayments(id);
    }
}
namespace UniWebKit
{
    public interface IEnvirProvider { }
    public interface IPlayerProvider : IDisposable { }
    public interface ILocalizationProvider : IDisposable { }
    public interface ILeaderboardProvider : IDisposable { }
    public class UnityEnvirProvider : IEnvirProvider { }
    public class LocalPrefsPlayerProvider : IPlayerProvider { public void Dispose() { } }
    public class LocalLocalizationProvider : ILocalizationProvider { public void Dispose() { } }
    public class UnsupportedLeaderboardProvider : ILeaderboardProvider { public void Dispose() { } }
}
