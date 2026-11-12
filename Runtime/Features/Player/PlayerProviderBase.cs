using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniWebKit
{
    public abstract class PlayerProviderBase : IPlayerProvider
    {
        private readonly IPlayerStorageBackend _backend;
        private readonly string _key, _cachePrefix;
        private readonly IPlayerDataSerializer _serializer;
        private readonly Dictionary<string, PlayerValue> _values = new Dictionary<string, PlayerValue>(StringComparer.Ordinal);
        private PlayerStorageOperation _operation;
        private string _loadedProfile;
        private int _version;
        private bool _loaded, _disposed, _dirty, _pendingCloud, _allowCloudWrite;

        protected PlayerProviderBase(IPlayerStorageBackend backend, PlayerStorageOptions options = null)
        {
            _backend = backend;
            _serializer = options?.Serializer;
            _key = string.IsNullOrWhiteSpace(options?.CloudKey) ? "UniWebKitSave" : options.CloudKey;
            _cachePrefix = string.IsNullOrEmpty(options?.LocalCachePrefix) ? "UniWebKit.Player." : options.LocalCachePrefix;
        }

        public bool IsSupported => !_disposed;
        public bool IsReady => !_disposed && _loaded && _loadedProfile == ProfileId;
        public bool IsBusy => _operation != null;
        public bool HasChanges => IsReady && _dirty;
        public bool HasPendingCloudChanges => IsReady && _pendingCloud;
        public bool CanConfirmCloudSaves => !_disposed && _backend != null && _backend.CanConfirmCloudWrites;
        public string ProfileId => string.IsNullOrEmpty(_backend?.ProfileId) ? "local" : _backend.ProfileId;
        public IReadOnlyCollection<string> Keys => IsReady ? _values.Keys : (IReadOnlyCollection<string>)Array.Empty<string>();

        public T Get<T>(string key, T defaultValue = default) => TryGet<T>(key, out var value) ? value : defaultValue;
        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            return IsReady && !string.IsNullOrEmpty(key) && _values.TryGetValue(key, out var stored) && stored.Decode(out value, _serializer);
        }
        public bool Set<T>(string key, T value)
        {
            if (!IsReady || string.IsNullOrEmpty(key) || !PlayerValue.Encode(value, out var encoded, _serializer)) return false;
            if (_values.TryGetValue(key, out var old) && old.type == encoded.type && old.data == encoded.data) return true;
            _values[key] = encoded; _version++; _dirty = true; return true;
        }
        public bool HasKey(string key) => IsReady && !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
        public bool DeleteKey(string key)
        {
            if (!HasKey(key)) return false;
            _values.Remove(key); _version++; _dirty = true; return true;
        }
        public void DeleteAll()
        {
            if (!IsReady || _values.Count == 0) return;
            _values.Clear(); _version++; _dirty = true;
        }

        private static PlayerStorageError Error(PlayerStorageErrorCode code, string message) => new PlayerStorageError(code, message);
        private string CacheKey(string profile) => _cachePrefix + (_backend?.Id ?? "local") + "."
            + Uri.EscapeDataString(profile) + "." + _key;

        private PlayerStorageOperation Begin(Action<PlayerStorageResult> callback)
        {
            var operation = new PlayerStorageOperation(callback);
            if (_disposed || IsBusy)
            {
                operation.Complete(new PlayerStorageResult(false, PlayerStorageLocation.None,
                    error: Error(_disposed ? PlayerStorageErrorCode.Disposed : PlayerStorageErrorCode.Busy,
                        _disposed ? "The storage provider is disposed." : "Another storage operation is pending.")));
                return operation;
            }
            _operation = operation;
            operation.OnCancel(() =>
            {
                if (!ReferenceEquals(_operation, operation)) return;
                _operation = null; _allowCloudWrite = false;
            });
            return operation;
        }
        private bool Owns(PlayerStorageOperation operation) => !_disposed && ReferenceEquals(_operation, operation) && !operation.IsCompleted;
        private void Finish(PlayerStorageOperation operation, PlayerStorageResult result)
        {
            if (!Owns(operation)) return;
            _operation = null;
            operation.Complete(result);
        }

        public PlayerStorageOperation Load(Action<PlayerStorageResult> onCompleted = null,
            PlayerStorageMode mode = PlayerStorageMode.Preferred,
            PlayerStorageConflictResolution conflictResolution = PlayerStorageConflictResolution.Fail)
        {
            var operation = Begin(onCompleted);
            if (operation.IsCompleted) return operation;
            string profileBefore = ProfileId;
            bool backendWasReady = _backend != null && _backend.IsAvailable;
            int versionBefore = _version;
            if (mode == PlayerStorageMode.LocalOnly || _backend == null)
            {
                LoadLocal(operation, profileBefore, mode, null);
                return operation;
            }

            try
            {
                _backend.Read(_key, reply =>
                {
                    if (!Owns(operation)) return;
                    string profile = ProfileId;
                    if (backendWasReady && profile != profileBefore)
                    { Finish(operation, Failure(PlayerStorageErrorCode.AccountChanged, "The player changed while loading.")); return; }
                    if (_version != versionBefore)
                    { Finish(operation, Failure(PlayerStorageErrorCode.Conflict, "Data changed while loading; existing values were preserved.")); return; }
                    if (!reply.IsSuccess)
                    { LoadLocal(operation, profile, mode, reply.Error); return; }
                    if (mode == PlayerStorageMode.CloudOnly && !reply.IsCloudConfirmed)
                    { LoadLocal(operation, profile, mode, Error(PlayerStorageErrorCode.Unsupported, "This backend cannot confirm cloud reads.")); return; }
                    if (!PlayerSaveDocument.TryRead(reply.Data, profile, out var remote, platformOwnsIdentity: true))
                    { LoadLocal(operation, profile, mode, Error(PlayerStorageErrorCode.InvalidData, "The platform save has an invalid format or owner.")); return; }

                    if (!TryReadCache(profile, out var local, out var cacheError))
                    {
                        if (remote == null) { Finish(operation, new PlayerStorageResult(false, PlayerStorageLocation.Local, error: cacheError)); return; }
                        local = null; // A validated platform copy can replace a broken local cache.
                    }
                    if (IsReady && _dirty) local = Capture(profile, true);
                    bool pending = local != null && local.pending;
                    bool differs = remote != null && local != null && remote.revision != local.revision;
                    if (pending && differs && conflictResolution == PlayerStorageConflictResolution.Fail)
                    {
                        Apply(local, profile); _allowCloudWrite = false;
                        Finish(operation, Failure(PlayerStorageErrorCode.Conflict,
                            "Unsent local changes differ from the platform save. Load with UseLocal or UseCloud to choose.", PlayerStorageLocation.Local));
                        return;
                    }
                    bool useLocal = local != null && (remote == null || pending && differs && conflictResolution == PlayerStorageConflictResolution.UseLocal);
                    var selected = useLocal ? local : remote;
                    Apply(selected, profile);
                    _allowCloudWrite = true;
                    _dirty = useLocal && selected.pending;
                    _pendingCloud = useLocal && selected.pending;
                    if (!useLocal && selected != null) { selected.pending = false; TryWriteCache(profile, selected, out _); }
                    OnLoaded(selected == null);
                    if (useLocal && mode == PlayerStorageMode.CloudOnly)
                    {
                        Finish(operation, Failure(PlayerStorageErrorCode.Conflict,
                            "Local changes are ready, but have not been confirmed in the cloud. Save them to upload.", PlayerStorageLocation.Local));
                        return;
                    }
                    Finish(operation, new PlayerStorageResult(true,
                        useLocal ? PlayerStorageLocation.Local : _backend.Location,
                        cloudConfirmed: !useLocal && reply.IsCloudConfirmed));
                });
            }
            catch (Exception exception)
            { LoadLocal(operation, ProfileId, mode, Error(PlayerStorageErrorCode.Failed, exception.Message)); }
            return operation;
        }

        private void LoadLocal(PlayerStorageOperation operation, string profile, PlayerStorageMode mode, PlayerStorageError cloudError)
        {
            if (!Owns(operation)) return;
            if (!TryReadCache(profile, out var local, out var cacheError))
            { Finish(operation, new PlayerStorageResult(false, PlayerStorageLocation.Local, error: cacheError)); return; }
            bool retainedChanges = IsReady && _dirty;
            if (retainedChanges) local = Capture(profile, true);
            Apply(local, profile);
            _dirty = retainedChanges;
            _allowCloudWrite = _backend == null;
            OnLoaded(local == null);
            if (mode == PlayerStorageMode.CloudOnly)
            {
                Finish(operation, new PlayerStorageResult(false, PlayerStorageLocation.Local,
                    error: cloudError ?? Error(PlayerStorageErrorCode.Unsupported, "Cloud storage is unavailable.")));
                return;
            }
            Finish(operation, new PlayerStorageResult(true, PlayerStorageLocation.Local, cloudError: cloudError));
        }

        protected virtual void OnLoaded(bool noDocument) { }

        public PlayerStorageOperation Save(Action<PlayerStorageResult> onCompleted = null,
            PlayerStorageMode mode = PlayerStorageMode.Preferred)
        {
            var operation = Begin(onCompleted);
            if (operation.IsCompleted) return operation;
            if (!IsReady)
            { Finish(operation, Failure(PlayerStorageErrorCode.NotReady, "Load the current player's data before saving.")); return operation; }
            string profile = ProfileId;
            int version = _version;
            var snapshot = Capture(profile, _backend != null);
            bool localSaved = TryWriteCache(profile, snapshot, out var localError);
            _pendingCloud = _backend != null;
            if (localSaved) _dirty = false;
            if (mode == PlayerStorageMode.LocalOnly || _backend == null)
            {
                Finish(operation, mode == PlayerStorageMode.CloudOnly
                    ? new PlayerStorageResult(false, PlayerStorageLocation.Local, localSaved: localSaved,
                        error: Error(PlayerStorageErrorCode.Unsupported, "Cloud storage is unavailable."))
                    : new PlayerStorageResult(localSaved, PlayerStorageLocation.Local, localSaved: localSaved, error: localError));
                return operation;
            }
            if (!_allowCloudWrite || !_backend.IsAvailable || mode == PlayerStorageMode.CloudOnly && !_backend.CanConfirmCloudWrites)
            {
                var error = Error(!_allowCloudWrite ? PlayerStorageErrorCode.Conflict : PlayerStorageErrorCode.Unsupported,
                    !_allowCloudWrite ? "Load platform data and resolve pending local changes before uploading."
                    : "Confirmed cloud saving is unavailable on this backend.");
                FinishFallback(operation, mode, localSaved, error, localError);
                return operation;
            }

            try
            {
                _backend.Write(_key, JsonUtility.ToJson(snapshot), reply =>
                {
                    if (!Owns(operation)) return;
                    if (profile != ProfileId)
                    { Finish(operation, Failure(PlayerStorageErrorCode.AccountChanged, "The player changed while saving.")); return; }
                    if (!reply.IsSuccess || mode == PlayerStorageMode.CloudOnly && !reply.IsCloudConfirmed)
                    {
                        FinishFallback(operation, mode, localSaved, reply.Error
                            ?? Error(PlayerStorageErrorCode.Unsupported, "The platform did not confirm cloud saving."), localError);
                        return;
                    }
                    snapshot.pending = false;
                    bool cached = TryWriteCache(profile, snapshot, out _);
                    if (version == _version) { _dirty = false; _pendingCloud = false; }
                    Finish(operation, new PlayerStorageResult(true, _backend.Location, reply.IsCloudConfirmed, cached));
                });
            }
            catch (Exception exception)
            { FinishFallback(operation, mode, localSaved, Error(PlayerStorageErrorCode.Failed, exception.Message), localError); }
            return operation;
        }

        private void FinishFallback(PlayerStorageOperation operation, PlayerStorageMode mode, bool localSaved,
            PlayerStorageError cloudError, PlayerStorageError localError)
        {
            Finish(operation, mode == PlayerStorageMode.CloudOnly || !localSaved
                ? new PlayerStorageResult(false, localSaved ? PlayerStorageLocation.Local : PlayerStorageLocation.None,
                    localSaved: localSaved, error: cloudError ?? localError)
                : new PlayerStorageResult(true, PlayerStorageLocation.Local, localSaved: true, cloudError: cloudError));
        }

        private static PlayerStorageResult Failure(PlayerStorageErrorCode code, string message,
            PlayerStorageLocation location = PlayerStorageLocation.None)
            => new PlayerStorageResult(false, location, error: Error(code, message));

        private bool TryReadCache(string profile, out PlayerSaveDocument document, out PlayerStorageError error)
        {
            document = null; error = null;
            try
            {
                string json = PlayerPrefs.GetString(CacheKey(profile), "");
                if (PlayerSaveDocument.TryRead(json, profile, out document)) return true;
                error = Error(PlayerStorageErrorCode.InvalidData, "The local cache is invalid; it was not overwritten.");
            }
            catch (Exception exception) { error = Error(PlayerStorageErrorCode.Failed, exception.Message); }
            return false;
        }
        private bool TryWriteCache(string profile, PlayerSaveDocument document, out PlayerStorageError error)
        {
            error = null;
            try { PlayerPrefs.SetString(CacheKey(profile), JsonUtility.ToJson(document)); PlayerPrefs.Save(); return true; }
            catch (Exception exception) { error = Error(PlayerStorageErrorCode.Failed, exception.Message); return false; }
        }

        private PlayerSaveDocument Capture(string profile, bool pending)
        {
            var document = new PlayerSaveDocument { owner = profile, revision = Guid.NewGuid().ToString("N"), pending = pending, entries = new List<PlayerSaveDocument.Entry>() };
            var keys = new List<string>(_values.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys) document.entries.Add(new PlayerSaveDocument.Entry { key = key, value = _values[key] });
            return document;
        }
        private void Apply(PlayerSaveDocument document, string profile)
        {
            _values.Clear();
            if (document != null) foreach (var entry in document.entries) _values.Add(entry.key, entry.value);
            _loaded = true; _loadedProfile = profile; _dirty = false;
            _pendingCloud = document != null && document.pending; _version++;
        }
        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _operation?.Cancel();
            _loaded = false; _values.Clear();
        }
    }
}
