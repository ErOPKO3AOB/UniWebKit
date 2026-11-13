using System;
using System.Collections.Generic;
using System.Globalization;

namespace UniWebKit
{
    public abstract class InAppProviderBase : IInAppProvider
    {
        private IReadOnlyList<InAppProduct> _products = Array.Empty<InAppProduct>();
        private IReadOnlyList<InAppPurchase> _purchases = Array.Empty<InAppPurchase>();
        private bool _productsChanged, _purchasesChanged;

        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public virtual bool IsReady => false;
        public virtual bool IsBusy => false;
        public virtual bool IsSubscriptionsSupported => false;
        public virtual InAppConsumeMode ConsumeMode => InAppConsumeMode.None;
        public IReadOnlyList<InAppProduct> Products => _products;
        public IReadOnlyList<InAppPurchase> Purchases => _purchases;
        public event Action<IReadOnlyList<InAppProduct>> ProductsChanged;
        public event Action<IReadOnlyList<InAppPurchase>> PurchasesChanged;

        public virtual void RefreshProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError);

        public virtual void RefreshPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null)
            => ReportUnsupported(onError);

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
            if (IsDisposed) return;
            var copy = new List<InAppProduct>();
            if (products != null)
                foreach (var product in products)
                    if (product != null) copy.Add(product);
            bool changed = copy.Count != _products.Count;
            for (int i = 0; !changed && i < copy.Count; i++) changed = !SameProduct(copy[i], _products[i]);
            if (!changed) return;
            _products = copy.AsReadOnly();
            _productsChanged = true;
        }

        protected void SetPurchases(IReadOnlyList<InAppPurchase> purchases)
        {
            if (IsDisposed) return;
            var copy = new List<InAppPurchase>();
            if (purchases != null)
                foreach (var purchase in purchases)
                    if (purchase != null) copy.Add(purchase);
            bool changed = copy.Count != _purchases.Count;
            for (int i = 0; !changed && i < copy.Count; i++) changed = !SamePurchase(copy[i], _purchases[i]);
            if (!changed) return;
            _purchases = copy.AsReadOnly();
            _purchasesChanged = true;
        }

        // Call after releasing the SDK operation and settling its callback, never halfway through a transaction.
        protected void NotifyChanges()
        {
            if (IsDisposed) return;
            bool products = _productsChanged, purchases = _purchasesChanged;
            _productsChanged = _purchasesChanged = false;
            if (products) UniWebCallbacks.Raise(ProductsChanged, _products);
            if (purchases && !IsDisposed) UniWebCallbacks.Raise(PurchasesChanged, _purchases);
        }

        private static bool SameProduct(InAppProduct a, InAppProduct b)
            => a.Id == b.Id && a.PlatformId == b.PlatformId && a.Title == b.Title && a.Description == b.Description &&
               a.ImageUrl == b.ImageUrl && a.SmallImageUrl == b.SmallImageUrl && a.Price == b.Price &&
               a.PriceText == b.PriceText && a.CurrencyCode == b.CurrencyCode && a.CurrencySymbol == b.CurrencySymbol &&
               a.CurrencyImageUrl == b.CurrencyImageUrl && a.Type == b.Type &&
               a.SubscriptionPeriodDays == b.SubscriptionPeriodDays && a.TrialPeriodDays == b.TrialPeriodDays;

        private static bool SamePurchase(InAppPurchase a, InAppPurchase b)
            => a.ProductId == b.ProductId && a.PlatformProductId == b.PlatformProductId && a.PurchaseId == b.PurchaseId &&
               a.PurchaseToken == b.PurchaseToken && a.Payload == b.Payload && a.CreatedAt == b.CreatedAt &&
               a.ExpiresAt == b.ExpiresAt && a.IsAutoRenewing == b.IsAutoRenewing && a.State == b.State;

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
            ProductsChanged = null;
            PurchasesChanged = null;
        }
    }
}
