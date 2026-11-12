using System;
using UnityEngine;

namespace UniWebKit
{
    [Serializable]
    public sealed class PlayerStorageOptions
    {
        [Tooltip("One string field in GamePush, or one storage key on other platforms.")]
        public string CloudKey = "UniWebKitSave";
        [NonSerialized] public IPlayerDataSerializer Serializer;
        public string LocalCachePrefix = "UniWebKit.Player.";
        [Min(1f)] public float TimeoutSeconds = 30f;
        [Tooltip("Optional native GamePush fields, for example unlockedLevels for leaderboards.")]
        public PlayerStorageFieldBinding[] GamePushFields = Array.Empty<PlayerStorageFieldBinding>();
    }

    public enum PlayerValueType { String, Int32, Int64, Single, Double, Decimal, Boolean, Json, Enum }

    [Serializable]
    public sealed class PlayerStorageFieldBinding
    {
        public string Key;
        public string PlatformField;
        public PlayerValueType Type = PlayerValueType.Int32;
    }
}
