using System;
using UniWebKit;
using UnityEngine;
using GamePush;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); checks++; }

    static void Main()
    {
        var local = new LocalLocalizationProvider();
        Check(local.Language == "ru" && local.IsReady && local.IsSupported, "system fallback");
        int changes = 0;
        local.LanguageChanged += _ => changes++;
        Check(local.SetLanguage(" PT_br ") && local.Language == "pt-BR", "regional normalization");
        Check(local.SetLanguage("pt-BR") && changes == 1, "duplicate suppression");
        foreach (var invalid in new[] { null, "", " ", "en--US", "1n", "en/ru", "x" })
            Check(!local.SetLanguage(invalid) && changes == 1, "reject invalid tag");
        Check(local.SetLanguage("zh_hant") && local.Language == "zh-Hant", "script tag");
        local.Dispose();
        Check(!local.SetLanguage("en") && !local.IsReady && !local.IsSupported, "disposed local");

        var gp = new GamePushLocalizationProvider();
        changes = 0;
        gp.LanguageChanged += _ => { Check(gp.IsReady, "ready before notification"); changes++; };
        Check(!gp.IsReady && !gp.SetLanguage("en"), "GP startup guard");
        GP_Init.isReady = true;
        MonoBehaviour.Tick();
        Check(gp.Language == "tr" && changes == 1 && MonoBehaviour.Pending == 0, "GP delayed initial language");
        Check(!gp.SetLanguage("uk") && !gp.SetLanguage("pt-BR") && gp.Language == "tr", "GP unsupported language");
        foreach (var code in new[] { "en", "ru", "tr", "fr", "it", "de", "es", "zh", "pt", "ko", "ja", "ar", "hi", "id" })
            Check(gp.SetLanguage(code) && gp.Language == code, "GP mapping " + code);
        GP_Language.Change(GamePush.Language.Russian);
        Check(gp.Language == "ru", "GP external change");
        gp.Dispose();
        GP_Language.Change(GamePush.Language.English);
        Check(gp.Language == "ru" && !gp.SetLanguage("en"), "GP unsubscription");

#if Localization_yg
        var yg = new PluginYG2LocalizationProvider();
        Check(!yg.IsReady && !yg.SetLanguage("en"), "YG startup guard");
        YG.YG2.onSwitchLang = null; // SDK resets delegates during initialization in Editor.
        YG.YG2.lang = "ru";
        changes = 0;
        yg.LanguageChanged += _ => changes++;
        YG.YG2.isSDKEnabled = true;
        MonoBehaviour.Tick();
        Check(yg.Language == "ru" && changes == 1, "YG initial notification even if fallback matches");
        Check(yg.SetLanguage("pt_br") && YG.YG2.lang == "pt-BR" && yg.Language == "pt-BR", "YG switching");
        YG.YG2.SwitchLanguage("de");
        Check(yg.Language == "de", "YG external change");
        yg.Dispose();
        Check(YG.YG2.onSwitchLang == null, "YG unsubscription");
#else
        using var yg = new PluginYG2LocalizationProvider();
        Check(yg.IsReady && yg.SetLanguage("uk") && yg.Language == "uk", "YG missing module fallback");
#endif
        GP_Init.isReady = false;
        var pending = new GamePushLocalizationProvider();
        Check(MonoBehaviour.Pending == 1, "waiting runner");
        pending.Dispose();
        Check(MonoBehaviour.Pending == 0, "dispose cancels startup");
        GP_Init.isReady = true;
        MonoBehaviour.Tick();
        Check(!pending.IsReady, "disposed startup never completes");

        var first = new LocalLocalizationProvider("en");
        UniWebBridge.Initialize(localization: first);
        UniWebBridge.Initialize(localization: first);
        Check(first.IsReady, "same instance retained");
        UniWebBridge.Initialize(localization: new LocalLocalizationProvider("ru"));
        Check(!first.IsSupported && UniWebBridge.Localization.Language == "ru", "replacement disposal");
        UniWebBridge.Initialize();
        Check(UniWebBridge.Localization.IsReady, "old Initialize overload behavior");
        Console.WriteLine($"PASS: {checks} localization checks");
    }
}
