#if UNIWEB_VK_GAMES
using System;

namespace UniWebKit
{
    // VK's order box has no client catalog, inventory or consume API.
    // A chargeable order remains Pending until a game's backend verifies and delivers it.
    public sealed class VKGamesInAppProvider : InAppProviderBase
    {
        private sealed class PendingOrder
        {
            internal VKGamesInAppProvider owner;
            internal Action<InAppPurchase> success;
            internal Action<InAppError> error;
            internal string product;
        }
        private static PendingOrder _pending;
        public override bool IsSupported => !IsDisposed && VKGamesRuntime.Supports("VKWebAppShowOrderBox");
        public override bool IsReady => IsSupported;
        public override bool IsBusy => _pending != null;

        public VKGamesInAppProvider()
        {
            if (VKGamesRuntime.IsBrowser) VKGamesRuntime.Request("init", null, _ => { });
        }

        public override void Purchase(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null)
        {
            InAppErrorCode? error = IsDisposed ? InAppErrorCode.Disposed : !IsReady ? InAppErrorCode.NotReady
                : IsBusy ? InAppErrorCode.Busy : string.IsNullOrWhiteSpace(productId) ? InAppErrorCode.InvalidArgument : (InAppErrorCode?)null;
            if (error.HasValue)
            {
                UniWebCallbacks.Invoke(() => onError?.Invoke(new InAppError(error.Value, "VK order cannot be started.", productId)));
                return;
            }
            _pending = new PendingOrder { owner = this, success = onSuccess, error = onError, product = productId };
            VKGamesRuntime.Request("purchase", productId, CompleteOrder, transaction: true);
        }

        private static void CompleteOrder(VKGamesRuntime.Reply reply)
        {
            var pending = _pending;
            if (pending == null) return;
            _pending = null;
            if (pending.owner == null) return;
            if (!reply.success)
            {
                var code = reply.status == "cancel" ? InAppErrorCode.Cancelled
                    : reply.Error.Code == UniWebErrorCode.Busy ? InAppErrorCode.Busy
                    : reply.Error.Code == UniWebErrorCode.Unsupported ? InAppErrorCode.Unsupported
                    : reply.Error.Code == UniWebErrorCode.InvalidArgument ? InAppErrorCode.InvalidArgument : InAppErrorCode.Failed;
                UniWebCallbacks.Invoke(() => pending.error?.Invoke(new InAppError(code, reply.error, pending.product)));
                return;
            }
            // No fabricated receipt, no Purchased state from an order-box notification.
            var purchase = new InAppPurchase(pending.product, reply.orderId, state: InAppPurchaseState.Pending);
            UniWebCallbacks.Invoke(() => pending.success?.Invoke(purchase));
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            base.Dispose();
            var pending = _pending;
            if (pending == null || !ReferenceEquals(pending.owner, this)) return;
            var callback = pending.error;
            pending.owner = null; pending.success = null; pending.error = null;
            UniWebCallbacks.Invoke(() => callback?.Invoke(new InAppError(InAppErrorCode.Disposed,
                "VK order box may still complete; verify orders on the backend.", pending.product)));
        }
    }
}
#endif
