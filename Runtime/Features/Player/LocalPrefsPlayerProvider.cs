namespace UniWebKit
{
    public sealed class LocalPrefsPlayerProvider : PlayerProviderBase
    {
        public LocalPrefsPlayerProvider() : this(null) { }
        public LocalPrefsPlayerProvider(PlayerStorageOptions options) : base(null, options) { }
    }

    public sealed class CustomPlayerProvider : PlayerProviderBase
    {
        public CustomPlayerProvider(IPlayerStorageBackend backend, PlayerStorageOptions options = null)
            : base(backend, options) { }
    }
}
