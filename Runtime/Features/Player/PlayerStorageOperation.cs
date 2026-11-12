using System;
using UnityEngine;

namespace UniWebKit
{
    public sealed class PlayerStorageOperation : IDisposable
    {
        private Action<PlayerStorageResult> _callback;
        private Action _onCancel;
        public bool IsCompleted { get; private set; }
        public PlayerStorageResult Result { get; private set; }

        internal PlayerStorageOperation(Action<PlayerStorageResult> callback) => _callback = callback;
        internal void OnCancel(Action cancel) => _onCancel = cancel;
        internal void Complete(PlayerStorageResult result)
        {
            if (IsCompleted) return;
            IsCompleted = true; Result = result;
            var callback = _callback; _callback = null; _onCancel = null;
            try { callback?.Invoke(result); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        public void Cancel()
        {
            if (IsCompleted) return;
            var cancel = _onCancel; _onCancel = null;
            cancel?.Invoke();
            Complete(new PlayerStorageResult(false, PlayerStorageLocation.None,
                error: new PlayerStorageError(PlayerStorageErrorCode.Cancelled, "The storage operation was cancelled.")));
        }
        public void Dispose() => Cancel();
    }
}
