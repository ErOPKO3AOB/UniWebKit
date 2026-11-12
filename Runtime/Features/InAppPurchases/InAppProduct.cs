namespace UniWebKit
{
    public sealed class InAppProduct
    {
        // Stable game-facing identifier. A GamePush tag is preferred over its numeric ID.
        public string Id { get; }
        public string PlatformId { get; }
        public string Title { get; }
        public string Description { get; }
        public string ImageUrl { get; }
        public string SmallImageUrl { get; }
        public decimal? Price { get; }
        public string PriceText { get; }
        public string CurrencyCode { get; }
        public string CurrencySymbol { get; }
        public string CurrencyImageUrl { get; }
        public InAppProductType Type { get; }
        public int? SubscriptionPeriodDays { get; }
        public int? TrialPeriodDays { get; }

        public InAppProduct(string id, string title = null, string description = null,
            decimal? price = null, string priceText = null, string currencyCode = null,
            string currencySymbol = null, string imageUrl = null, string smallImageUrl = null,
            string currencyImageUrl = null, string platformId = null,
            InAppProductType type = InAppProductType.Unknown,
            int? subscriptionPeriodDays = null, int? trialPeriodDays = null)
        {
            Id = id ?? string.Empty;
            PlatformId = platformId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Price = price;
            PriceText = priceText ?? string.Empty;
            CurrencyCode = currencyCode ?? string.Empty;
            CurrencySymbol = currencySymbol ?? string.Empty;
            ImageUrl = imageUrl ?? string.Empty;
            SmallImageUrl = smallImageUrl ?? string.Empty;
            CurrencyImageUrl = currencyImageUrl ?? string.Empty;
            Type = type;
            SubscriptionPeriodDays = subscriptionPeriodDays;
            TrialPeriodDays = trialPeriodDays;
        }
    }
}
