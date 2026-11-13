using System;
using System.Collections.Generic;
using System.Reflection;
using UniWebKit;

static class Program
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    sealed class Platform : PlatformProviderBase { public void Ready() => MarkInitialized(); }
    sealed class Profile : ProfileProviderBase { public void Push(ProfileInfo data) => SetProfile(data); }
    sealed class Pause : PauseProviderBase { public void Native(bool value) => SetPlatformPaused(value); }
    sealed class Store : InAppProviderBase
    {
        public void ProductsSnapshot(params InAppProduct[] values) { SetProducts(values); NotifyChanges(); }
        public void PurchaseSnapshot(params InAppPurchase[] values) { SetPurchases(values); NotifyChanges(); }
    }

    static void Main()
    {
        var vkEnvironment = new VKGamesEnvirProvider();
        Check(vkEnvironment.IsDesktop && vkEnvironment.IsReady, "VK Editor uses Unity desktop classification");
        UnityEngine.SystemInfo.deviceType = UnityEngine.DeviceType.Handheld;
        Check(vkEnvironment.IsMobile && !vkEnvironment.IsDesktop, "VK Editor handheld fallback");
        UnityEngine.SystemInfo.deviceType = UnityEngine.DeviceType.Unknown;
        Check(!vkEnvironment.IsReady && !vkEnvironment.IsMobile && !vkEnvironment.IsDesktop, "unknown device does not become desktop");
        UnityEngine.SystemInfo.deviceType = UnityEngine.DeviceType.Desktop;
        var vkLanguage = new VKGamesLocalizationProvider();
        Check(vkLanguage.Language == "ru" && vkLanguage.IsReady, "VK Editor system language fallback");
        var synchronize = typeof(VKGamesLocalizationProvider).GetMethod("SynchronizeLanguage", BindingFlags.Instance | BindingFlags.NonPublic);
        int languageChanges = 0;
        vkLanguage.LanguageChanged += _ => languageChanges++;
        UnityEngine.Application.systemLanguage = UnityEngine.SystemLanguage.English;
        synchronize.Invoke(vkLanguage, null);
        Check(vkLanguage.Language == "en" && languageChanges == 1, "automatic language update");
        Check(vkLanguage.SetLanguage("PT_br") && vkLanguage.Language == "pt-BR", "VK local language selection");
        UnityEngine.Application.systemLanguage = UnityEngine.SystemLanguage.Russian;
        synchronize.Invoke(vkLanguage, null);
        Check(vkLanguage.Language == "pt-BR" && languageChanges == 2, "late SDK language does not overwrite game choice");
        Check(!vkLanguage.SetLanguage("??") && languageChanges == 2, "invalid locale rejected");
        vkLanguage.Dispose(); synchronize.Invoke(vkLanguage, null);
        Check(!vkLanguage.IsReady && !vkLanguage.SetLanguage("ru") && languageChanges == 2, "disposed VK locale stays detached");
        var platform = new Platform();
        int ready = 0;
        platform.Initialized += () => { Check(platform.IsReady, "ready before notification"); ready++; };
        platform.Ready(); platform.Ready();
        Check(ready == 1, "initialization event once");
        platform.Dispose(); platform.Ready();
        Check(!platform.IsReady && ready == 1, "disposed platform stays unready");
        using var vkPlatform = new VKGamesPlatformProvider();
        UniWebError error = null;
        vkPlatform.Initialize(onError: value => error = value);
        Check(!vkPlatform.IsSupported && error.Code == UniWebErrorCode.Unsupported, "Editor platform fallback");
        using var vkProfile = new VKGamesProfileProvider();
        vkProfile.Refresh(onError: value => error = value);
        Check(!vkProfile.IsReady && error.Code == UniWebErrorCode.Unsupported, "Editor profile fallback");

        var profile = new Profile();
        int changes = 0;
        profile.ProfileChanged += _ => changes++;
        profile.Push(new ProfileInfo("1", "Name"));
        profile.Push(new ProfileInfo("1", "Name"));
        Check(profile.IsReady && changes == 1, "same profile is not a change");
        profile.Push(new ProfileInfo("2", "Other"));
        Check(changes == 2 && profile.CurrentProfile.Id == "2", "account comparison");
        profile.ProfileChanged += _ => throw new Exception("subscriber");
        profile.ProfileChanged += _ => changes++;
        profile.Push(new ProfileInfo("2", "New name"));
        Check(changes == 4 && UnityEngine.Debug.Exceptions == 1, "one subscriber cannot block others");
        profile.Dispose(); profile.Push(new ProfileInfo("3"));
        Check(!profile.IsReady && profile.CurrentProfile == null, "profile disposal");

        var pause = new Pause();
        var states = new List<bool>();
        pause.PauseChanged += value => states.Add(value);
        pause.Native(true); pause.SetPaused(true); pause.Native(false);
        Check(pause.IsPaused && states.Count == 1, "native resume does not clear manual pause");
        pause.SetPaused(false); pause.Native(true); pause.SetPaused(false);
        Check(pause.IsPaused && states.Count == 3, "manual resume does not clear platform pause");
        pause.Native(false);
        Check(!pause.IsPaused && states.Count == 4, "all reasons cleared");
        pause.Dispose(); pause.SetPaused(true);
        Check(!pause.IsPaused && states.Count == 4, "disposed pause does not notify");
        var game = new UnsupportedGameProvider();
        game.GameReady(); game.GameplayStart(); game.GameplayStop();
        Check(!game.IsSupported, "no fake VK game lifecycle");

        var store = new Store();
        int products = 0, purchases = 0;
        store.ProductsChanged += _ => products++;
        store.PurchasesChanged += _ => purchases++;
        var first = new InAppProduct("coins", price: 1);
        store.ProductsSnapshot(first);
        var snapshot = store.Products;
        store.ProductsSnapshot(new InAppProduct("coins", price: 1));
        Check(products == 1, "equal catalog does not notify twice");
        store.ProductsSnapshot(new InAppProduct("coins", price: 2));
        Check(products == 2 && snapshot[0].Price == 1, "catalog change and previous snapshot");
        store.PurchaseSnapshot(new InAppPurchase("coins", "id", state: InAppPurchaseState.Pending));
        Check(purchases == 1 && !store.HasPurchase("coins"), "pending order never grants ownership");
        store.PurchaseSnapshot(new InAppPurchase("coins", "id"));
        Check(purchases == 2 && store.HasPurchase("coins"), "purchase state change notifies");
        InAppError inAppError = null;
        store.RefreshProducts(onError: value => inAppError = value);
        Check(inAppError.Code == InAppErrorCode.Unsupported && store.Products.Count == 1, "unsupported force refresh keeps cache");
        Check((int)InAppConsumeMode.Automatic == 3 && (int)InAppConsumeMode.PurchaseId == 4, "consume modes preserve existing values");
        store.Dispose(); store.ProductsSnapshot(first);
        Check(products == 2 && store.Products.Count == 0, "store disposal detaches subscribers");
        using var vkStore = new VKGamesInAppProvider();
        vkStore.FetchPurchases(onError: value => inAppError = value);
        Check(inAppError.Code == InAppErrorCode.Unsupported, "VK inventory explicitly unsupported");
        vkStore.Consume(new InAppPurchase("coins", "receipt"), onError: value => inAppError = value);
        Check(inAppError.Code == InAppErrorCode.Unsupported && vkStore.ConsumeMode == InAppConsumeMode.None, "VK cannot pretend to consume");

        // Deliver a native order callback to the real provider; the client must never claim Purchased.
        var pendingType = typeof(VKGamesInAppProvider).GetNestedType("PendingOrder", BindingFlags.NonPublic);
        var pending = Activator.CreateInstance(pendingType);
        InAppPurchase order = null;
        foreach (var item in new (string, object)[] { ("owner", vkStore), ("product", "coins"), ("success", (Action<InAppPurchase>)(value => order = value)) })
            pendingType.GetField(item.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pending, item.Item2);
        typeof(VKGamesInAppProvider).GetField("_pending", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, pending);
        typeof(VKGamesInAppProvider).GetMethod("CompleteOrder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
            new object[] { new VKGamesRuntime.Reply { success = true, orderId = "real-id", status = "chargeable" } });
        Check(order.State == InAppPurchaseState.Pending && order.PurchaseId == "real-id", "chargeable is Pending with real ID");
        int lateSuccess = 0, disposedErrors = 0;
        var abandoned = Activator.CreateInstance(pendingType);
        foreach (var item in new (string, object)[] { ("owner", vkStore), ("product", "coins"),
            ("success", (Action<InAppPurchase>)(_ => lateSuccess++)),
            ("error", (Action<InAppError>)(e => { if (e.Code == InAppErrorCode.Disposed) disposedErrors++; })) })
            pendingType.GetField(item.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(abandoned, item.Item2);
        typeof(VKGamesInAppProvider).GetField("_pending", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, abandoned);
        vkStore.Dispose();
        Check(vkStore.IsBusy && disposedErrors == 1, "disposed payment still holds native lane");
        typeof(VKGamesInAppProvider).GetMethod("CompleteOrder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
            new object[] { new VKGamesRuntime.Reply { success = true, orderId = "late-id", status = "chargeable" } });
        Check(!vkStore.IsBusy && lateSuccess == 0 && disposedErrors == 1, "late order cannot reach disposed game callbacks");
        YG.YG2.isSDKEnabled = true;
        YG.YG2.purchases = new[] { new YG.Product { id = "coins", price = "1", priceValue = "1" } };
        var ygStore = new PluginYG2InAppProvider();
        int catalogChanges = 0, inventoryChanges = 0;
        ygStore.ProductsChanged += _ => catalogChanges++;
        ygStore.PurchasesChanged += _ => inventoryChanges++;
        ygStore.FetchProducts(); ygStore.FetchProducts();
        Check(catalogChanges == 1 && inventoryChanges == 1, "existing SDK snapshots publish deduplicated events");
        ygStore.PurchasesChanged += _ => ygStore.Dispose();
        int paid = 0;
        ygStore.Purchase("coins", _ => paid++);
        Check(paid == 1 && !ygStore.IsBusy && inventoryChanges == 2, "snapshot event disposal happens after SDK lane cleanup");

        var localPause = new LocalPauseProvider();
        UniWebBridge.Initialize(profile: profile, pause: localPause);
        UniWebBridge.Initialize(pause: localPause);
        localPause.SetPaused(true);
        Check(localPause.IsPaused, "same pause instance retained");
        UniWebBridge.Initialize();
        localPause.SetPaused(true);
        Check(!localPause.IsSupported && !UniWebBridge.Platform.IsSupported && !UniWebBridge.Game.IsSupported,
            "bridge replaces providers and restores fallbacks");
Console.WriteLine($"PASS: {checks} platform/profile/pause/IAP/environment/localization checks");
    }
}
