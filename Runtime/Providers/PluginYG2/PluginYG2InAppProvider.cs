#if UNIWEB_PLUGIN_YG2
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using YG;

namespace UniWebKit
{
    public sealed class PluginYG2InAppProvider : InAppProviderBase
    {
#if Payments_yg
        private static PluginYG2InAppProvider _operationOwner;
        private static Action _drainCleanup;
        private Action _cleanup;
        private Action _onDisposed;
        private string _pendingId;
#if UNITY_EDITOR
        // The SDK's Editor consume method is a no-op. Keep simulation state outside SDK assets.
        private static readonly HashSet<string> EditorConsumedIds = new HashSet<string>(StringComparer.Ordinal);
#endif

        // YG2 does not expose a cross-platform payments-availability flag. Only advertise a
        // usable store once its selected backend has supplied a non-empty catalog.
        public override bool IsSupported => !IsDisposed && YG2.isSDKEnabled &&
            YG2.purchases != null && YG2.purchases.Length > 0;
        public override bool IsReady => IsSupported;
        public override bool IsBusy => _operationOwner != null || _drainCleanup != null;
        public override InAppConsumeMode ConsumeMode => InAppConsumeMode.Automatic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _operationOwner?.Dispose();
            _drainCleanup?.Invoke();
#if UNITY_EDITOR
            EditorConsumedIds.Clear();
#endif
        }

        public override void FetchProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null)
        {
            if (!CheckReady(onError)) return;
            RefreshSnapshots();
            onSuccess?.Invoke(Products);
        }

        public override void FetchPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null)
        {
            if (!CheckReady(onError)) return;
            RefreshSnapshots();
            onSuccess?.Invoke(Purchases);
        }

        public override void Purchase(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null)
            => StartOperation(productId, false, onSuccess, onError);

        public override void Consume(InAppPurchase purchase, Action onSuccess = null, Action<InAppError> onError = null)
        {
            if (purchase == null || purchase.State != InAppPurchaseState.Purchased)
            {
                onError?.Invoke(new InAppError(InAppErrorCode.InvalidArgument, "A confirmed purchase is required.", purchase?.ProductId));
                return;
            }
            StartOperation(purchase.ProductId, true, _ => onSuccess?.Invoke(), onError);
        }

        private void StartOperation(string productId, bool consume, Action<InAppPurchase> onSuccess, Action<InAppError> onError)
        {
            if (!CheckReady(onError)) return;
            if (string.IsNullOrWhiteSpace(productId))
            {
                onError?.Invoke(new InAppError(InAppErrorCode.InvalidArgument, "A product ID is required.", productId));
                return;
            }
            if (IsBusy)
            {
                onError?.Invoke(new InAppError(InAppErrorCode.Busy, "Another PluginYG2 purchase operation is still pending.", productId));
                return;
            }

            RefreshSnapshots();
            if (!TryGetProduct(productId, out var product))
            {
                onError?.Invoke(new InAppError(InAppErrorCode.ProductNotFound, "The product is not in the SDK catalog.", productId));
                return;
            }
            if (consume && !HasPurchase(productId))
            {
                onError?.Invoke(new InAppError(InAppErrorCode.PurchaseNotFound,
                    "No unconsumed purchase is exposed by PluginYG2. A new purchase is consumed automatically by this SDK.", productId));
                return;
            }

#if UNITY_EDITOR
            if (consume)
            {
                EditorConsumedIds.Add(productId);
                RefreshSnapshots();
                onSuccess?.Invoke(new InAppPurchase(productId, platformProductId: productId));
                return;
            }
#endif

            void OnSuccess(string id)
            {
                if (!string.Equals(id, product.Id, StringComparison.Ordinal)) return;
#if UNITY_EDITOR
                EditorConsumedIds.Add(id);
#endif
                // YG2's success notification also initiates consumption; it carries no receipt token.
                RefreshSnapshots();
                Cleanup();
                onSuccess?.Invoke(new InAppPurchase(id, platformProductId: id));
            }

            void OnError(string id)
            {
                if (!string.Equals(id, product.Id, StringComparison.Ordinal)) return;
                Cleanup();
                onError?.Invoke(new InAppError(InAppErrorCode.Failed,
                    "PluginYG2 reported a payment failure. Its API does not expose a separate cancellation reason.", id));
            }

            void Cleanup()
            {
                YG2.onPurchaseSuccess -= OnSuccess;
                YG2.onPurchaseFailed -= OnError;
                ReleaseOperation();
            }

            _operationOwner = this;
            _pendingId = product.Id;
            _cleanup = Cleanup;
            _onDisposed = () => onError?.Invoke(new InAppError(InAppErrorCode.Disposed,
                "The provider was disposed. The SDK operation may still complete.", product.Id));
            YG2.onPurchaseSuccess += OnSuccess;
            YG2.onPurchaseFailed += OnError;
            try
            {
                if (consume) YG2.ConsumePurchaseByID(product.Id, true);
                else YG2.BuyPayments(product.Id);
            }
            catch (Exception exception)
            {
                // A synchronous completion can invoke client code and start another operation.
                if (_cleanup != (Action)Cleanup) { Debug.LogException(exception); return; }
                Cleanup();
                onError?.Invoke(new InAppError(InAppErrorCode.Failed, exception.Message, product.Id));
            }
        }

        private bool CheckReady(Action<InAppError> onError)
        {
            if (IsDisposed)
            {
                onError?.Invoke(new InAppError(InAppErrorCode.Disposed, "The purchases provider has been disposed."));
                return false;
            }
            if (IsReady) return true;
            onError?.Invoke(new InAppError(InAppErrorCode.NotReady,
                "PluginYG2 has not exposed a payment catalog. Wait for SDK initialization; the selected platform may not support payments."));
            return false;
        }

        private void RefreshSnapshots()
        {
            var products = new List<InAppProduct>();
            var purchases = new List<InAppPurchase>();
            var source = YG2.purchases;
            if (source != null)
            {
                foreach (var entry in source)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    // Keep the platform-formatted text. Parse only priceValue, never the text with currency.
                    var value = (entry.priceValue ?? string.Empty).Replace(',', '.');
                    decimal? price = decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed : (decimal?)null;
                    products.Add(new InAppProduct(entry.id, entry.title, entry.description, price, entry.price,
                        entry.priceCurrencyCode, imageUrl: entry.imageURI, currencyImageUrl: entry.currencyImageURL,
                        platformId: entry.id));
                    if (entry.consumed) continue;
#if UNITY_EDITOR
                    if (EditorConsumedIds.Contains(entry.id)) continue;
#endif
                    purchases.Add(new InAppPurchase(entry.id, platformProductId: entry.id));
                }
            }
            SetProducts(products);
            SetPurchases(purchases);
        }

        private void ReleaseOperation()
        {
            _cleanup = null;
            _onDisposed = null;
            _pendingId = null;
            if (ReferenceEquals(_operationOwner, this)) _operationOwner = null;
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            var callback = _onDisposed;
            var pendingId = _pendingId;
            base.Dispose();
            _cleanup?.Invoke();
            ReleaseOperation();
            if (pendingId != null) DrainOperation(pendingId);
            callback?.Invoke();
        }

        // No disposed provider or client callback is retained while an SDK operation finishes.
        private static void DrainOperation(string productId)
        {
            void OnFinished(string id)
            {
                if (string.Equals(id, productId, StringComparison.Ordinal)) Cleanup();
            }
            void Cleanup()
            {
                YG2.onPurchaseSuccess -= OnFinished;
                YG2.onPurchaseFailed -= OnFinished;
                _drainCleanup = null;
            }
            _drainCleanup = Cleanup;
            YG2.onPurchaseSuccess += OnFinished;
            YG2.onPurchaseFailed += OnFinished;
        }
#else
        // PluginYG2's Payments module is optional. Keep the normal safe fallback contract.
        public override bool IsSupported => false;
#endif
    }
}
#endif
