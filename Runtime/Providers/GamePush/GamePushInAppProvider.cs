#if UNIWEB_GAMEPUSH
using System;
using System.Collections.Generic;
using System.Globalization;
using GamePush;
using UnityEngine;
using UnityEngine.Events;

namespace UniWebKit
{
    public sealed class GamePushInAppProvider : InAppProviderBase
    {
        // GamePush's error events have no request ID. Serialize transactions across provider instances.
        private static GamePushInAppProvider _operationOwner;
        private static Action _drainCleanup;
        private enum Operation { None, Purchase, Consume, Subscribe, Unsubscribe }
        private Operation _pendingOperation;
        private string _pendingId;
        private string _pendingPlatformId;
        private Action _cleanup;
        private Action _onDisposed;

        public override bool IsSupported
        {
            get
            {
                if (IsDisposed || !GP_Init.isReady) return false;
                try { return GP_Payments.IsPaymentsAvailable(); }
                catch (Exception) { return false; }
            }
        }

        public override bool IsReady => IsSupported;
        public override bool IsBusy => _operationOwner != null || _drainCleanup != null;
        public override InAppConsumeMode ConsumeMode => InAppConsumeMode.ProductId;
        public override bool IsSubscriptionsSupported
        {
            get
            {
                if (!IsReady) return false;
                try { return GP_Payments.IsSubscriptionsAvailable(); }
                catch (Exception) { return false; }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _operationOwner?.Dispose();
            _drainCleanup?.Invoke();
        }

        // Current GamePush SDKs preload these lists. Avoid the deprecated Fetch API and its global callbacks.
        public override void FetchProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null)
        {
            if (!CheckReady(onError)) return;
            try { SetProducts(ReadProducts()); }
            catch (Exception exception) { onError?.Invoke(Failed(exception)); return; }
            onSuccess?.Invoke(Products);
        }

        public override void FetchPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null)
        {
            if (!CheckReady(onError)) return;
            try { SetPurchases(ReadPurchases()); }
            catch (Exception exception) { onError?.Invoke(Failed(exception)); return; }
            onSuccess?.Invoke(Purchases);
        }

        public override void Purchase(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null)
            => StartPurchase(productId, false, onSuccess, onError);

        public override void Subscribe(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null)
            => StartPurchase(productId, true, onSuccess, onError);

        private void StartPurchase(string productId, bool subscription, Action<InAppPurchase> onSuccess, Action<InAppError> onError)
        {
            if (!ValidateId(productId, onError) || !TryBegin(onError)) return;
            if (subscription && !IsSubscriptionsSupported)
            {
                EndWithError(onError, new InAppError(InAppErrorCode.Unsupported, "Subscriptions are not supported.", productId));
                return;
            }

            InAppProduct product;
            var knownPurchaseIds = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                SetProducts(ReadProducts());
                SetPurchases(ReadPurchases());
                TryGetProduct(productId, out product);
                foreach (var purchase in Purchases)
                    if (!string.IsNullOrEmpty(purchase.PurchaseId)) knownPurchaseIds.Add(purchase.PurchaseId);
            }
            catch (Exception exception) { EndWithError(onError, Failed(exception, productId)); return; }

            if (product == null)
            {
                EndWithError(onError, new InAppError(InAppErrorCode.ProductNotFound, "The product is not in the SDK catalog.", productId));
                return;
            }

            if ((product.Type == InAppProductType.Subscription) != subscription)
            {
                EndWithError(onError, new InAppError(InAppErrorCode.InvalidArgument,
                    subscription ? "The product is not a subscription." : "Use Subscribe for a subscription product.", productId));
                return;
            }

            void OnSuccess(string idOrTag)
            {
                if (!MatchesProduct(idOrTag, product.Id, product.PlatformId)) return;
                // Payment success is authoritative even if reading the updated inventory fails.
                var receipt = ReadConfirmedPurchase(product, knownPurchaseIds, subscription);
                Cleanup();
                onSuccess?.Invoke(receipt);
            }

            void OnError()
            {
                Cleanup();
                onError?.Invoke(new InAppError(InAppErrorCode.Failed,
                    "GamePush did not complete the payment. Its Unity API does not distinguish cancellation from other errors.", product.Id));
            }

            void Cleanup()
            {
                Detach(subscription ? Operation.Subscribe : Operation.Purchase, OnSuccess, OnError);
                ReleaseOperation();
            }

            _cleanup = Cleanup;
            _pendingOperation = subscription ? Operation.Subscribe : Operation.Purchase;
            _pendingId = product.Id;
            _pendingPlatformId = product.PlatformId;
            _onDisposed = () => onError?.Invoke(new InAppError(InAppErrorCode.Disposed,
                "The provider was disposed while awaiting payment. Fetch purchases on the next initialization to recover the result.", product.Id));
            Attach(_pendingOperation, OnSuccess, OnError);
            if (subscription)
            {
                InvokeSdk(() => GP_Payments.Subscribe(product.Id), onError, product.Id);
            }
            else
            {
                InvokeSdk(() => GP_Payments.Purchase(product.Id), onError, product.Id);
            }
        }

        public override void Consume(InAppPurchase purchase, Action onSuccess = null, Action<InAppError> onError = null)
        {
            if (purchase == null || purchase.State != InAppPurchaseState.Purchased)
            {
                onError?.Invoke(new InAppError(InAppErrorCode.InvalidArgument, "A confirmed purchase is required.", purchase?.ProductId));
                return;
            }
            StartInventoryOperation(purchase.ProductId, purchase, onSuccess, onError);
        }

        public override void Unsubscribe(string productId, Action onSuccess = null, Action<InAppError> onError = null)
            => StartInventoryOperation(productId, null, onSuccess, onError);

        private void StartInventoryOperation(string productId, InAppPurchase purchase, Action onSuccess, Action<InAppError> onError)
        {
            if (!ValidateId(productId, onError) || !TryBegin(onError)) return;
            var consume = purchase != null;
            if (!consume && !IsSubscriptionsSupported)
            {
                EndWithError(onError, new InAppError(InAppErrorCode.Unsupported, "Subscriptions are not supported.", productId));
                return;
            }

            InAppProduct product;
            try
            {
                SetProducts(ReadProducts());
                TryGetProduct(productId, out product);
            }
            catch (Exception exception) { EndWithError(onError, Failed(exception, productId)); return; }
            if (consume && product != null &&
                (product.Type == InAppProductType.NonConsumable || product.Type == InAppProductType.Subscription))
            {
                EndWithError(onError, new InAppError(InAppErrorCode.InvalidArgument,
                    "Permanent products and subscriptions must not be consumed.", productId));
                return;
            }
            if (!consume && product != null && product.Type != InAppProductType.Subscription)
            {
                EndWithError(onError, new InAppError(InAppErrorCode.InvalidArgument, "The product is not a subscription.", productId));
                return;
            }
            var id = product?.Id ?? productId;
            var platformId = product?.PlatformId ?? purchase?.PlatformProductId;

            void OnSuccess(string idOrTag)
            {
                if (!MatchesProduct(idOrTag, id, platformId)) return;
                try { SetPurchases(ReadPurchases()); }
                catch (Exception exception)
                {
                    // Do not turn an acknowledged consume/unsubscribe into a failed transaction.
                    Debug.LogException(exception);
                    if (consume) RemoveCachedPurchase(purchase);
                }
                Cleanup();
                onSuccess?.Invoke();
            }

            void OnError()
            {
                Cleanup();
                onError?.Invoke(new InAppError(InAppErrorCode.Failed,
                    consume ? "GamePush could not consume the purchase." : "GamePush could not cancel the subscription renewal.", id));
            }

            void Cleanup()
            {
                Detach(consume ? Operation.Consume : Operation.Unsubscribe, OnSuccess, OnError);
                ReleaseOperation();
            }

            _cleanup = Cleanup;
            _pendingOperation = consume ? Operation.Consume : Operation.Unsubscribe;
            _pendingId = id;
            _pendingPlatformId = platformId;
            _onDisposed = () => onError?.Invoke(new InAppError(InAppErrorCode.Disposed,
                "The provider was disposed. The platform operation may still complete; reload the inventory before retrying.", id));
            Attach(_pendingOperation, OnSuccess, OnError);
            if (consume)
            {
                InvokeSdk(() => GP_Payments.Consume(id), onError, id);
            }
            else
            {
                InvokeSdk(() => GP_Payments.Unsubscribe(id), onError, id);
            }
        }

        private bool CheckReady(Action<InAppError> onError)
        {
            InAppError error = null;
            if (IsDisposed) error = new InAppError(InAppErrorCode.Disposed, "The purchases provider has been disposed.");
            else if (!GP_Init.isReady) error = new InAppError(InAppErrorCode.NotReady, "Wait for the platform SDK to initialize.");
            else
            {
                try
                {
                    if (!GP_Payments.IsPaymentsAvailable())
                        error = new InAppError(InAppErrorCode.Unsupported, "Purchases are not available on the current platform.");
                }
                catch (Exception exception) { error = Failed(exception); }
            }
            if (error == null) return true;
            onError?.Invoke(error);
            return false;
        }

        private bool TryBegin(Action<InAppError> onError)
        {
            if (!CheckReady(onError)) return false;
            if (_operationOwner != null || _drainCleanup != null)
            {
                onError?.Invoke(new InAppError(InAppErrorCode.Busy, "Another GamePush purchase operation is still pending."));
                return false;
            }
            _operationOwner = this;
            return true;
        }

        private static bool ValidateId(string productId, Action<InAppError> onError)
        {
            if (!string.IsNullOrWhiteSpace(productId)) return true;
            onError?.Invoke(new InAppError(InAppErrorCode.InvalidArgument, "A product ID is required.", productId));
            return false;
        }

        private void ReleaseOperation()
        {
            _cleanup = null;
            _onDisposed = null;
            _pendingOperation = Operation.None;
            _pendingId = null;
            _pendingPlatformId = null;
            if (ReferenceEquals(_operationOwner, this)) _operationOwner = null;
        }

        private void EndWithError(Action<InAppError> onError, InAppError error)
        {
            _cleanup?.Invoke();
            ReleaseOperation();
            onError?.Invoke(error);
        }

        private void InvokeSdk(Action sdkCall, Action<InAppError> onError, string productId)
        {
            var cleanup = _cleanup;
            try { sdkCall(); }
            catch (Exception exception)
            {
                // A synchronous SDK completion can invoke client code and even start the next operation.
                if (_cleanup != cleanup) { Debug.LogException(exception); return; }
                EndWithError(onError, Failed(exception, productId));
            }
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            var callback = _onDisposed;
            var operation = _pendingOperation;
            var id = _pendingId;
            var platformId = _pendingPlatformId;
            base.Dispose();
            _cleanup?.Invoke();
            ReleaseOperation();
            if (operation != Operation.None) DrainOperation(operation, id, platformId);
            callback?.Invoke();
        }

        // Disposing cannot cancel a platform payment. Keep the SDK lane occupied until its
        // terminal event, without retaining the disposed provider or any of the client's callbacks.
        private static void DrainOperation(Operation operation, string id, string platformId)
        {
            void OnSuccess(string idOrTag)
            {
                if (MatchesProduct(idOrTag, id, platformId)) Cleanup();
            }
            void OnError() => Cleanup();
            void Cleanup()
            {
                Detach(operation, OnSuccess, OnError);
                _drainCleanup = null;
            }
            _drainCleanup = Cleanup;
            Attach(operation, OnSuccess, OnError);
        }

        private static void Attach(Operation operation, UnityAction<string> onSuccess, UnityAction onError)
        {
            switch (operation)
            {
                case Operation.Purchase: GP_Payments.OnPurchaseSuccess += onSuccess; GP_Payments.OnPurchaseError += onError; break;
                case Operation.Consume: GP_Payments.OnConsumeSuccess += onSuccess; GP_Payments.OnConsumeError += onError; break;
                case Operation.Subscribe: GP_Payments.OnSubscribeSuccess += onSuccess; GP_Payments.OnSubscribeError += onError; break;
                case Operation.Unsubscribe: GP_Payments.OnUnsubscribeSuccess += onSuccess; GP_Payments.OnUnsubscribeError += onError; break;
            }
        }

        private static void Detach(Operation operation, UnityAction<string> onSuccess, UnityAction onError)
        {
            switch (operation)
            {
                case Operation.Purchase: GP_Payments.OnPurchaseSuccess -= onSuccess; GP_Payments.OnPurchaseError -= onError; break;
                case Operation.Consume: GP_Payments.OnConsumeSuccess -= onSuccess; GP_Payments.OnConsumeError -= onError; break;
                case Operation.Subscribe: GP_Payments.OnSubscribeSuccess -= onSuccess; GP_Payments.OnSubscribeError -= onError; break;
                case Operation.Unsubscribe: GP_Payments.OnUnsubscribeSuccess -= onSuccess; GP_Payments.OnUnsubscribeError -= onError; break;
            }
        }

        private InAppPurchase ReadConfirmedPurchase(InAppProduct product, HashSet<string> knownIds, bool subscription)
        {
            try
            {
                SetPurchases(ReadPurchases());
                foreach (var purchase in Purchases)
                    if (purchase.State == InAppPurchaseState.Purchased &&
                        MatchesProduct(product.Id, purchase.ProductId, purchase.PlatformProductId) &&
                        !string.IsNullOrEmpty(purchase.PurchaseId) &&
                        (subscription || !knownIds.Contains(purchase.PurchaseId))) return purchase;
            }
            catch (Exception exception) { Debug.LogException(exception); }

            // Some GamePush versions only return an ID/tag on success. Do not reuse an older receipt ID.
            var receipt = new InAppPurchase(product.Id, platformProductId: product.PlatformId);
            foreach (var purchase in Purchases)
                if (MatchesProduct(product.Id, purchase.ProductId, purchase.PlatformProductId)) return receipt;
            var purchases = new List<InAppPurchase>(Purchases) { receipt };
            SetPurchases(purchases);
            return receipt;
        }

        private void RemoveCachedPurchase(InAppPurchase consumed)
        {
            var purchases = new List<InAppPurchase>(Purchases);
            for (var i = 0; i < purchases.Count; i++)
            {
                var candidate = purchases[i];
                if ((!string.IsNullOrEmpty(consumed.PurchaseId) && candidate.PurchaseId == consumed.PurchaseId) ||
                    (string.IsNullOrEmpty(consumed.PurchaseId) &&
                     MatchesProduct(consumed.ProductId, candidate.ProductId, candidate.PlatformProductId)))
                {
                    purchases.RemoveAt(i);
                    break;
                }
            }
            SetPurchases(purchases);
        }

        private static List<InAppProduct> ReadProducts()
        {
            var result = new List<InAppProduct>();
            var products = GP_Payments.GetProducts();
            if (products == null) return result;
            foreach (var product in products)
            {
                if (product == null) continue;
                var platformId = product.id.ToString(CultureInfo.InvariantCulture);
                var id = string.IsNullOrEmpty(product.tag) ? platformId : product.tag;
                decimal? price = decimal.TryParse(product.price.ToString("R", CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
                var currency = string.IsNullOrEmpty(product.currencySymbol) ? product.currency : product.currencySymbol;
                var priceText = price.HasValue
                    ? price.Value.ToString(CultureInfo.CurrentCulture) + (string.IsNullOrEmpty(currency) ? "" : " " + currency)
                    : string.Empty;
                result.Add(new InAppProduct(id, product.name, product.description, price, priceText,
                    product.currency, product.currencySymbol, product.icon, product.iconSmall,
                    platformId: platformId, type: product.isSubscription ? InAppProductType.Subscription : InAppProductType.Unknown,
                    subscriptionPeriodDays: product.isSubscription ? product.period : (int?)null,
                    trialPeriodDays: product.isSubscription ? product.trialPeriod : (int?)null));
            }
            return result;
        }

        private static List<InAppPurchase> ReadPurchases()
        {
            var result = new List<InAppPurchase>();
            var purchases = GP_Payments.GetPurchases();
            if (purchases == null) return result;
            foreach (var purchase in purchases)
            {
                if (purchase == null) continue;
                var platformId = purchase.productId.ToString(CultureInfo.InvariantCulture);
                var id = string.IsNullOrEmpty(purchase.tag) ? platformId : purchase.tag;
                var state = InAppPurchaseState.Purchased;
                if (!string.IsNullOrEmpty(purchase.orderStatus) &&
                    !string.Equals(purchase.orderStatus, "paid", StringComparison.OrdinalIgnoreCase))
                    state = string.Equals(purchase.orderStatus, "canceled", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(purchase.orderStatus, "cancelled", StringComparison.OrdinalIgnoreCase)
                        ? InAppPurchaseState.Cancelled : InAppPurchaseState.Pending;
                result.Add(new InAppPurchase(id, purchase.purchaseId, platformProductId: platformId,
                    payload: purchase.payload, createdAt: purchase.createdAt, expiresAt: purchase.expiredAt,
                    isAutoRenewing: purchase.subscribed, state: state));
            }
            return result;
        }

        private static InAppError Failed(Exception exception, string productId = null)
            => new InAppError(InAppErrorCode.Failed, exception.Message, productId);
    }
}
#endif
