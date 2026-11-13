namespace UniWebKit
{
    public interface IGameProvider : IUniWebFeature
    {
        void GameReady();
        void GameplayStart();
        void GameplayStop();
    }
}
