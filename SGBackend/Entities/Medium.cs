namespace SGBackend.Entities;

public enum MediumSource
{
    Spotify
}

public class Medium : BaseEntity
{
    public string Title { get; set; }

    public MediumSource MediumSource { get; set; }

    public string LinkToMedium { get; set; }

    public bool ExplicitContent { get; set; }

    public List<Artist> Artists { get; set; }

    public List<MediumImage> Images { get; set; }

    public string AlbumName { get; set; }

    public string ReleaseDate { get; set; }
}