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

        // Callbacks may complete synchronously, including unsupported/not-ready results.
        void FetchProducts(Action<IReadOnlyList<InAppProduct>> onSuccess = null, Action<InAppError> onError = null);
        void FetchPurchases(Action<IReadOnlyList<InAppPurchase>> onSuccess = null, Action<InAppError> onError = null);
        bool TryGetProduct(string productId, out InAppProduct product);
        bool HasPurchase(string productId);

        void Purchase(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null);

        // Grant and persist the reward before consuming. Permanent purchases must remain unconsumed.
        // Consult ConsumeMode: not every SDK can target an individual receipt.
        void Consume(InAppPurchase purchase, Action onSuccess = null, Action<InAppError> onError = null);

        void Subscribe(string productId, Action<InAppPurchase> onSuccess = null, Action<InAppError> onError = null);
        void Unsubscribe(string productId, Action onSuccess = null, Action<InAppError> onError = null);
    }
}
