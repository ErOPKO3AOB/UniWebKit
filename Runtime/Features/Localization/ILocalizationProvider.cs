using System;

namespace UniWebKit
{
    public interface ILocalizationProvider : IUniWebFeature, IDisposable
    {
        bool IsReady { get; }
        string Language { get; }
        // Raised on SDK readiness and subsequent language changes, on Unity's main thread.
        event Action<string> LanguageChanged;
        // True means accepted, not an asynchronous platform acknowledgement.
        bool SetLanguage(string language);
    }
}
