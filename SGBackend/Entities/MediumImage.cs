namespace SGBackend.Entities;

public class MediumImage : BaseEntity
{
    public string imageUrl { get; set; }

    public int height { get; set; }

    public int width { get; set; }
}