#if UNIWEB_VK_GAMES
using System;

namespace UniWebKit
{
    // Direct VK Bridge storage, where the host exposes window.vkBridge.
    // VK Play desktop cloud saves are a different API.
    public sealed class VKGamesPlayerProvider : PlayerProviderBase
    {
        public VKGamesPlayerProvider() : this(null) { }
        public VKGamesPlayerProvider(PlayerStorageOptions options) : base(new Backend(options), options) { }

        private sealed class Backend : IPlayerStorageBackend
        {
            private readonly float _timeout;
            public Backend(PlayerStorageOptions options) => _timeout = options?.TimeoutSeconds ?? 30f;
            public string Id => "vk";
            public string ProfileId => PlayerStorageRuntime.VkProfileId;
            public bool IsAvailable => PlayerStorageRuntime.VkAvailable;
            public bool CanConfirmCloudWrites => IsAvailable;
            public PlayerStorageLocation Location => PlayerStorageLocation.Cloud;
            public void Read(string key, Action<PlayerStorageBackendResult> done)
                => PlayerStorageRuntime.WhenReady(() => IsAvailable, () =>
                    PlayerStorageRuntime.Request(Id, "read", key, ProfileId, null, null, _timeout, done), done, _timeout);
            public void Write(string key, string document, Action<PlayerStorageBackendResult> done)
                => PlayerStorageRuntime.Request(Id, "write", key, ProfileId, document, null, _timeout, done);
        }
    }
}
#endif
