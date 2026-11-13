namespace UniWebKit
{
    public sealed class ProfileInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string AvatarUrl { get; }
        public ProfileInfo(string id, string name = null, string avatarUrl = null)
        { Id = id ?? string.Empty; Name = name ?? string.Empty; AvatarUrl = avatarUrl ?? string.Empty; }
    }
}
