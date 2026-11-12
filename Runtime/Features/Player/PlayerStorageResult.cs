namespace UniWebKit
{
    public enum PlayerStorageMode { Preferred, LocalOnly, CloudOnly }
    public enum PlayerStorageLocation { None, Local, Platform, Cloud }
    public enum PlayerStorageConflictResolution { Fail, UseCloud, UseLocal }
    public enum PlayerStorageErrorCode
    {
        None, Unsupported, NotReady, Busy, Disposed, Cancelled, Timeout, InvalidData,
        MissingConfiguration, AccountChanged, Conflict, QuotaExceeded, Failed, RateLimited
    }

    public sealed class PlayerStorageError
    {
        public PlayerStorageErrorCode Code { get; }
        public string Message { get; }
        public PlayerStorageError(PlayerStorageErrorCode code, string message)
        { Code = code; Message = message ?? string.Empty; }
        public override string ToString() => Code + ": " + Message;
    }

    public sealed class PlayerStorageResult
    {
        public bool IsSuccess { get; }
        public PlayerStorageLocation Location { get; }
        public bool IsCloudConfirmed { get; }
        public bool IsLocalSaved { get; }
        public bool IsFallback => IsSuccess && CloudError != null;
        public PlayerStorageError Error { get; }
        public PlayerStorageError CloudError { get; }

        public PlayerStorageResult(bool success, PlayerStorageLocation location,
            bool cloudConfirmed = false, bool localSaved = false,
            PlayerStorageError error = null, PlayerStorageError cloudError = null)
        {
            IsSuccess = success; Location = location; IsCloudConfirmed = cloudConfirmed;
            IsLocalSaved = localSaved; Error = error; CloudError = cloudError;
        }
    }
}
