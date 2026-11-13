using System;
using System.Collections.Generic;

namespace UniWebKit
{
    public interface IInAppProvider : IUniWebFeature, IDisposable
    {
        bool IsReady { get; }
        bool IsBusy { get; }
        bool IsSubscriptionsSupported { get; }
        InAppConsumeMode ConsumeMode { get; }

        IReadOnlyList<InAppProduct> Products { get; }
        IReadOnlyList<InAppPurchase> Purchases { get; }
        event Action<IReadOnlyList<InAppProduct>> ProductsChanged;
        event Action<IReadOnlyList<InAppPurchase>> PurchasesChanged;

        // Callbacks may complete synchronously, including unsupported/not-ready results.
        void FetchProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null);
        void FetchPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null);
        // Unlike Fetch, these request a platform refresh. Unsupported when the SDK cannot do so.
        void RefreshProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null);
        void RefreshPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null);
        bool TryGetProduct(string productId, out InAppProduct product);
        bool HasPurchase(string productId);

        // onSuccess can carry Pending; only Purchased confirms ownership. VK orders require server verification.
        void Purchase(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null);

        // For manual consume modes, grant and persist the reward before consuming.
        // Automatic means that the SDK starts consumption during Purchase; do not consume it twice.
        // Permanent purchases must remain unconsumed; an Automatic SDK may not support that flow.
        void Consume(InAppPurchase purchase, Action onSuccess = null, Action<InAppError> onError = null);

        void Subscribe(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null);
        void Unsubscribe(string productId, Action onSuccess = null, Action<InAppError> onError = null);
    }
}
