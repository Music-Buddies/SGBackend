namespace SGBackend.Entities;

public enum Language
{
    de,
    en
}

public class User : BaseEntity
{
    public string Name { get; set; }

    public string? SpotifyId { get; set; }

    public string? SpotifyProfileUrl { get; set; }

    public string? SpotifyRefreshToken { get; set; }

    public List<PlaybackRecord> PlaybackRecords { get; set; } = new();

    public List<PlaybackSummary> PlaybackSummaries { get; set; } = new();

    public Stats Stats { get; set; } = new();

    // default locale is eng
    public Language Language { get; set; } = Language.de;

    public ModelUser ToModelUser()
    {
        return new ModelUser
        {
            Id = Id,
            Name = Name,
            SpotifyId = SpotifyId,
            SpotifyProfileUrl = SpotifyProfileUrl
        };
    }

    public ExportUser ToExportUser()
    {
        return new ExportUser
        {
            Name = Name,
            SpotifyId = SpotifyId,
            SpotifyProfileUrl = SpotifyProfileUrl,
            SpotifyRefreshToken = SpotifyRefreshToken,
            PlaybackRecords = PlaybackRecords.Select(pr => pr.ToExportPlaybackRecord()).ToList()
        };
    }
}

public class ExportUser
{
    public string Name { get; set; }

    public string? SpotifyId { get; set; }

    public string? SpotifyProfileUrl { get; set; }

    public string? SpotifyRefreshToken { get; set; }

    public List<ExportPlaybackRecord> PlaybackRecords { get; set; } = new();

    public User ToUser(Dictionary<string, Guid> mediumLinkMap)
    {
        return new User
        {
            SpotifyId = SpotifyId,
            SpotifyProfileUrl = SpotifyProfileUrl,
            SpotifyRefreshToken = SpotifyRefreshToken,
            PlaybackRecords = PlaybackRecords.Select(p => p.ToPlaybackRecord(mediumLinkMap)).ToList(),
            Name = Name
        };
    }
}

public class ModelUser
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string SpotifyId { get; set; }

    public string SpotifyProfileUrl { get; set; }
}