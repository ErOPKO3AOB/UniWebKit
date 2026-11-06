namespace UniWebKit
{
    public interface IPlayerProvider
    {
        void Save(string data);
        string Load();
    }
}