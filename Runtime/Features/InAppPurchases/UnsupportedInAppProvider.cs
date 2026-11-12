namespace UniWebKit
{
    public sealed class UnsupportedInAppProvider : InAppProviderBase
    {
        public override bool IsSupported => false;
    }
}
