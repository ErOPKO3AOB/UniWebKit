namespace UniWebKit
{
    public abstract class GameProviderBase : IGameProvider
    {
        public virtual bool IsSupported => true;
        public virtual void GameReady() { }
        public virtual void GameplayStart() { }
        public virtual void GameplayStop() { }
    }

    public sealed class UnsupportedGameProvider : GameProviderBase
    {
        public override bool IsSupported => false;
    }
}
