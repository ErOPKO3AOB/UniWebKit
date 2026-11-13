using System;

namespace UniWebKit
{
    public abstract class LocalizationProviderBase : ILocalizationProvider
    {
        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public bool IsReady { get; private set; }
        public string Language { get; private set; }
        public event Action<string> LanguageChanged;

        protected LocalizationProviderBase(string language = "en", bool isReady = true)
        {
            Language = NormalizeLanguage(language) ?? "en";
            IsReady = isReady;
        }

        public virtual bool SetLanguage(string language)
        {
            var code = NormalizeLanguage(language);
            if (IsDisposed || !IsReady || code == null) return false;
            ApplyLanguage(code);
            return true;
        }

        protected void ApplyLanguage(string language)
        {
            var code = NormalizeLanguage(language);
            if (IsDisposed || code == null) return;
            bool notify = !IsReady || Language != code;
            Language = code;
            IsReady = true;
            if (notify) LanguageChanged?.Invoke(code);
        }

        // Preserve regional tags; do not constrain game languages to any SDK enum.
        public static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            var parts = language.Trim().Replace('_', '-').Split('-');
            if (parts[0].Length < 2 || parts[0].Length > 8) return null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 8) return null;
                foreach (char c in parts[i])
                    if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || i > 0 && c >= '0' && c <= '9')) return null;
                parts[i] = i == 0 ? parts[i].ToLowerInvariant()
                    : parts[i].Length == 2 ? parts[i].ToUpperInvariant()
                    : parts[i].Length == 4 ? char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant()
                    : parts[i].ToLowerInvariant();
            }
            return string.Join("-", parts);
        }

        public virtual void Dispose()
        {
            IsDisposed = true;
            IsReady = false;
            LanguageChanged = null;
        }
    }
}
