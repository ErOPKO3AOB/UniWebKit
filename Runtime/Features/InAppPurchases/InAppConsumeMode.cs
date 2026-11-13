namespace UniWebKit
{
    public enum InAppConsumeMode
    {
        None,
        ProductId,
        PurchaseToken,
        // The SDK initiates consumption during purchase. Save-before-consume cannot be guaranteed.
        Automatic,
        // Consume the exact InAppPurchase.PurchaseId, never every purchase of that product.
        PurchaseId
    }
}
