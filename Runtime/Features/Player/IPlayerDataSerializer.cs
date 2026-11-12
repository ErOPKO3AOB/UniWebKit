namespace UniWebKit
{
    // Optional adapter for serializers such as Newtonsoft.Json, without a package dependency.
    // Both methods must use the same format and return false for unsupported or corrupt values.
    public interface IPlayerDataSerializer
    {
        bool TrySerialize<T>(T value, out string json);
        bool TryDeserialize<T>(string json, out T value);
    }
}
