using System;
using System.Collections.Generic;
using System.Globalization;

namespace UniWebKit
{
    public abstract class InAppProviderBase : IInAppProvider
    {
        private IReadOnlyList<InAppProduct> _products = Array.Empty<InAppProduct>();
        private IReadOnlyList<InAppPurchase> _purchases = Array.Empty<InAppPurchase>();

        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public virtual bool IsReady => false;
        public virtual bool IsBusy => false;
        public virtual bool IsSubscriptionsSupported => false;
        public virtual InAppConsumeMode ConsumeMode => InAppConsumeMode.None;
        public IReadOnlyList<InAppProduct> Products => _products;
        public IReadOnlyList<InAppPurchase> Purchases => _purchases;

        public virtual void FetchProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError);

        public virtual void FetchPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError);

        public virtual void Purchase(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError, productId);

        public virtual void Consume(InAppPurchase purchase, Action onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError, purchase?.ProductId);

        public virtual void Subscribe(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError, productId);

        public virtual void Unsubscribe(string productId, Action onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError, productId);

        public bool TryGetProduct(string productId, out InAppProduct product)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                foreach (var item in Products)
                {
                    if (MatchesProduct(productId, item.Id, item.PlatformId))
                    {
                        product = item;
                        return true;
                    }
                }
            }
            product = null;
            return false;
        }

        // The cache describes current ownership/unconsumed purchases, not purchase history.
        public bool HasPurchase(string productId)
        {
            if (IsDisposed || string.IsNullOrEmpty(productId)) return false;
            foreach (var purchase in Purchases)
            {
                if (purchase.State != InAppPurchaseState.Purchased ||
                    !MatchesProduct(productId, purchase.ProductId, purchase.PlatformProductId)) continue;
                if (!string.IsNullOrEmpty(purchase.ExpiresAt) &&
                    DateTimeOffset.TryParse(purchase.ExpiresAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow) continue;
                return true;
            }
            return false;
        }

        protected void SetProducts(IReadOnlyList<InAppProduct> products)
        {
            var copy = new List<InAppProduct>();
            if (products != null)
                foreach (var product in products)
                    if (product != null) copy.Add(product);
            _products = copy.AsReadOnly();
        }

        protected void SetPurchases(IReadOnlyList<InAppPurchase> purchases)
        {
            var copy = new List<InAppPurchase>();
            if (purchases != null)
                foreach (var purchase in purchases)
                    if (purchase != null) copy.Add(purchase);
            _purchases = copy.AsReadOnly();
        }

        protected static bool MatchesProduct(string requestedId, string id, string platformId)
            => !string.IsNullOrEmpty(requestedId) &&
               (string.Equals(requestedId, id, StringComparison.Ordinal) ||
                string.Equals(requestedId, platformId, StringComparison.Ordinal));

        protected void ReportUnsupported(Action<InAppError> onError, string productId = null)
            => onError?.Invoke(new InAppError(IsDisposed ? InAppErrorCode.Disposed : InAppErrorCode.Unsupported,
                IsDisposed ? "The purchases provider has been disposed." : "This operation is not supported.", productId));

        public virtual void Dispose()
        {
            IsDisposed = true;
            _products = Array.Empty<InAppProduct>();
            _purchases = Array.Empty<InAppPurchase>();
        }
    }
}
