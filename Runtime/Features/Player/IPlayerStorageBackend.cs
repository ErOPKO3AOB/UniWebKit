using System;

namespace UniWebKit
{
    // Custom SDK integrations implement this contract; game code keeps using IPlayerProvider.
    public interface IPlayerStorageBackend
    {
        string Id { get; }
        string ProfileId { get; }
        bool IsAvailable { get; }
        bool CanConfirmCloudWrites { get; }
        PlayerStorageLocation Location { get; }
        void Read(string key, Action<PlayerStorageBackendResult> onCompleted);
        void Write(string key, string document, Action<PlayerStorageBackendResult> onCompleted);
    }

    public sealed class PlayerStorageBackendResult
    {
        public bool IsSuccess { get; }
        public string Data { get; }
        public bool IsCloudConfirmed { get; }
        public PlayerStorageError Error { get; }
        public PlayerStorageBackendResult(string data = null, bool cloudConfirmed = false,
            PlayerStorageError error = null)
        { Data = data; IsCloudConfirmed = cloudConfirmed; Error = error; IsSuccess = error == null; }
    }
}
