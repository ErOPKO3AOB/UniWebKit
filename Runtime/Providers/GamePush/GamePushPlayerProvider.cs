#if UNIWEB_GAMEPUSH
using System;
using System.Globalization;
using GamePush;
using UnityEngine;

namespace UniWebKit
{
    public sealed class GamePushPlayerProvider : PlayerProviderBase
    {
        private readonly PlayerStorageFieldBinding[] _bindings;
        public GamePushPlayerProvider() : this(null) { }
        public GamePushPlayerProvider(PlayerStorageOptions options) : base(new Backend(options), options)
            => _bindings = options?.GamePushFields ?? Array.Empty<PlayerStorageFieldBinding>();

        protected override void OnLoaded(bool noDocument)
        {
            if (!noDocument || !GP_Init.isReady) return;
            // Initial migration from existing native fields; the portable document then owns these values.
            foreach (var binding in _bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.Key) || string.IsNullOrEmpty(binding.PlatformField)) continue;
                switch (binding.Type)
                {
                    case PlayerValueType.Int32: Set(binding.Key, GP_Player.GetInt(binding.PlatformField)); break;
                    case PlayerValueType.Single: Set(binding.Key, GP_Player.GetFloat(binding.PlatformField)); break;
                    case PlayerValueType.Boolean: Set(binding.Key, GP_Player.GetBool(binding.PlatformField)); break;
                    case PlayerValueType.String: Set(binding.Key, GP_Player.GetString(binding.PlatformField)); break;
                }
            }
        }

        private sealed class Backend : IPlayerStorageBackend
        {
            private readonly float _timeout;
            private readonly PlayerStorageFieldBinding[] _bindings;
            [Serializable] private sealed class BindingList { public PlayerStorageFieldBinding[] bindings; }
            public Backend(PlayerStorageOptions options)
            {
                _timeout = options?.TimeoutSeconds ?? 30f;
                _bindings = options?.GamePushFields ?? Array.Empty<PlayerStorageFieldBinding>();
            }
            public string Id => "gamepush";
            public string ProfileId => GP_Init.isReady ? GP_Player.GetID().ToString(CultureInfo.InvariantCulture) : "guest";
            public bool IsAvailable => GP_Init.isReady;
#if UNITY_WEBGL && !UNITY_EDITOR
            public bool CanConfirmCloudWrites => true;
            public PlayerStorageLocation Location => PlayerStorageLocation.Cloud;
#else
            public bool CanConfirmCloudWrites => false;
            public PlayerStorageLocation Location => PlayerStorageLocation.Platform;
#endif

            public void Read(string key, Action<PlayerStorageBackendResult> done)
                => PlayerStorageRuntime.WhenReady(() => GP_Init.isReady, () =>
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    PlayerStorageRuntime.Request(Id, "read", key, ProfileId, null, null, _timeout, done);
#else
                    done(new PlayerStorageBackendResult(GP_Player.GetString(key)));
#endif
                }, done, _timeout);

            public void Write(string key, string document, Action<PlayerStorageBackendResult> done)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                PlayerStorageRuntime.Request(Id, "write", key, ProfileId, document,
                    JsonUtility.ToJson(new BindingList { bindings = _bindings }), _timeout, done);
#else
                // Editor/native API exposes only shared sync events. Do not report them as a per-request cloud ACK.
                GP_Player.Set(key, document);
                if (PlayerSaveDocument.TryRead(document, ProfileId, out var parsed) && parsed != null)
                    foreach (var binding in _bindings)
                    {
                        if (binding == null || string.IsNullOrEmpty(binding.PlatformField)) continue;
                        var entry = parsed.entries.Find(item => item.key == binding.Key);
                        if (entry == null) continue;
                        switch (binding.Type)
                        {
                            case PlayerValueType.Int32:
                                if (entry.value.Decode<int>(out var integer)) GP_Player.Set(binding.PlatformField, integer);
                                break;
                            case PlayerValueType.Single:
                                if (entry.value.Decode<float>(out var number)) GP_Player.Set(binding.PlatformField, number);
                                break;
                            case PlayerValueType.Boolean:
                                if (entry.value.Decode<bool>(out var flag)) GP_Player.Set(binding.PlatformField, flag);
                                break;
                            default: GP_Player.Set(binding.PlatformField, entry.value.data); break;
                        }
                    }
                GP_Player.Sync();
                done(new PlayerStorageBackendResult());
#endif
            }
        }
    }
}
#endif
