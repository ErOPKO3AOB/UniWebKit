using System;
using System.Collections.Generic;

namespace UniWebKit
{
    public interface IPlayerProvider : IUniWebFeature, IDisposable
    {
        bool IsReady { get; }
        bool IsBusy { get; }
        bool HasChanges { get; }
        bool HasPendingCloudChanges { get; }
        bool CanConfirmCloudSaves { get; }
        string ProfileId { get; }
        IReadOnlyCollection<string> Keys { get; }

        T Get<T>(string key, T defaultValue = default);
        bool TryGet<T>(string key, out T value);
        bool Set<T>(string key, T value);
        bool HasKey(string key);
        bool DeleteKey(string key);
        void DeleteAll();

        // Operations may finish synchronously. Cancel does not undo a server write already in flight.
        PlayerStorageOperation Load(Action<PlayerStorageResult> onCompleted = null,
            PlayerStorageMode mode = PlayerStorageMode.Preferred,
            PlayerStorageConflictResolution conflictResolution = PlayerStorageConflictResolution.Fail);
        PlayerStorageOperation Save(Action<PlayerStorageResult> onCompleted = null,
            PlayerStorageMode mode = PlayerStorageMode.Preferred);
    }
}
