#if UNIWEB_UNITASK
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniWebKit
{
    public static class PlayerProviderUniTaskExtensions
    {
        public static UniTask<PlayerStorageResult> LoadAsync(this IPlayerProvider player,
            PlayerStorageMode mode = PlayerStorageMode.Preferred,
            PlayerStorageConflictResolution conflictResolution = PlayerStorageConflictResolution.Fail,
            CancellationToken cancellationToken = default)
            => AwaitOperation(done => player.Load(done, mode, conflictResolution), cancellationToken);

        public static UniTask<PlayerStorageResult> SaveAsync(this IPlayerProvider player,
            PlayerStorageMode mode = PlayerStorageMode.Preferred, CancellationToken cancellationToken = default)
            => AwaitOperation(done => player.Save(done, mode), cancellationToken);

        private static async UniTask<PlayerStorageResult> AwaitOperation(
            Func<Action<PlayerStorageResult>, PlayerStorageOperation> start, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new PlayerStorageResult(false, PlayerStorageLocation.None,
                    error: new PlayerStorageError(PlayerStorageErrorCode.Cancelled, "The storage operation was cancelled."));
            var completion = new UniTaskCompletionSource<PlayerStorageResult>();
            var operation = start(result => completion.TrySetResult(result));
            using (token.Register(() => operation.Cancel()))
                return await completion.Task;
        }
    }
}
#endif
