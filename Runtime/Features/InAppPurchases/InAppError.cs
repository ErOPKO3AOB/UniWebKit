namespace UniWebKit
{
    public sealed class InAppError
    {
        public InAppErrorCode Code { get; }
        public string Message { get; }
        public string ProductId { get; }

        public InAppError(InAppErrorCode code, string message, string productId = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            ProductId = productId ?? string.Empty;
        }

        public override string ToString() => $"{Code}: {Message}";
    }
}
