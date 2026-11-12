namespace UniWebKit
{
    public sealed class InAppPurchase
    {
        public string ProductId { get; }
        public string PlatformProductId { get; }
        // Empty when the SDK does not expose a transaction identifier. Never a fabricated product ID.
        public string PurchaseId { get; }
        public string PurchaseToken { get; }
        public string Payload { get; }
        public string CreatedAt { get; }
        public string ExpiresAt { get; }
        // Renewal can be disabled while the entitlement remains valid until ExpiresAt.
        public bool? IsAutoRenewing { get; }
        public InAppPurchaseState State { get; }

        public InAppPurchase(string productId, string purchaseId = null, string purchaseToken = null,
            string platformProductId = null, string payload = null, string createdAt = null,
            string expiresAt = null, bool? isAutoRenewing = null,
            InAppPurchaseState state = InAppPurchaseState.Purchased)
        {
            ProductId = productId ?? string.Empty;
            PlatformProductId = platformProductId ?? string.Empty;
            PurchaseId = purchaseId ?? string.Empty;
            PurchaseToken = purchaseToken ?? string.Empty;
            Payload = payload ?? string.Empty;
            CreatedAt = createdAt ?? string.Empty;
            ExpiresAt = expiresAt ?? string.Empty;
            IsAutoRenewing = isAutoRenewing;
            State = state;
        }
    }
}
