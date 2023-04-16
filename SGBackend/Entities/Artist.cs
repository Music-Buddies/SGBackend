namespace SGBackend.Entities;

public class Artist : BaseEntity
{
    public string Name { get; set; }

    public ExportArtist ToExportArtist()
    {
        return new ExportArtist
        {
            Name = Name
        };
    }
}

public class ExportArtist
{
    public string Name { get; set; }

    public Artist ToArtist()
    {
        return new Artist
        {
            Name = Name
        };
    }
}