#if UNIWEB_PLUGIN_YG2
using System;
using YG;

namespace UniWebKit
{
    public sealed class PluginYG2PlayerProvider : PlayerProviderBase
    {
        public PluginYG2PlayerProvider() : this(null) { }
        public PluginYG2PlayerProvider(PlayerStorageOptions options) : base(new Backend(options), options) { }

        private sealed class Backend : IPlayerStorageBackend
        {
            private readonly float _timeout;
            public Backend(PlayerStorageOptions options) => _timeout = options?.TimeoutSeconds ?? 30f;
            public string Id => "pluginyg2";
            public string ProfileId
            {
                get
                {
#if Authorization_yg
                    return YG2.isSDKEnabled ? Convert.ToString(YG2.player.id, System.Globalization.CultureInfo.InvariantCulture) : "guest";
#else
                    return "guest";
#endif
                }
            }
            public bool IsAvailable
            {
                get
                {
#if Storage_yg && RedefinePlayerPrefs_yg
                    return YG2.isSDKEnabled;
#else
                    return false;
#endif
                }
            }
            // SaveProgress has no success/error completion event in the supported SDK.
            public bool CanConfirmCloudWrites => false;
            public PlayerStorageLocation Location => PlayerStorageLocation.Platform;

            public void Read(string key, Action<PlayerStorageBackendResult> done)
            {
#if Storage_yg && RedefinePlayerPrefs_yg
                PlayerStorageRuntime.WhenReady(() => YG2.isSDKEnabled, () =>
                    done(new PlayerStorageBackendResult(RedefineYG.PlayerPrefs.GetString(key, ""))), done, _timeout);
#else
                done(PlayerStorageRuntime.Failure(PlayerStorageErrorCode.Unsupported,
                    "PluginYG2 storage requires the Storage and RedefinePlayerPrefs modules. Local storage remains available."));
#endif
            }
            public void Write(string key, string document, Action<PlayerStorageBackendResult> done)
            {
#if Storage_yg && RedefinePlayerPrefs_yg
                RedefineYG.PlayerPrefs.SetString(key, document);
                YG2.SaveProgress();
                done(new PlayerStorageBackendResult());
#else
                done(PlayerStorageRuntime.Failure(PlayerStorageErrorCode.Unsupported, "PluginYG2 storage modules are unavailable."));
#endif
            }
        }
    }
}
#endif
