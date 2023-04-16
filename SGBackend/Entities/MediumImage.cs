namespace SGBackend.Entities;

public class MediumImage : BaseEntity
{
    public string imageUrl { get; set; }

    public int height { get; set; }

    public int width { get; set; }

    public ExportMediumImage ToExportImage()
    {
        return new ExportMediumImage
        {
            width = width,
            height = height,
            imageUrl = imageUrl
        };
    }
}

public class ExportMediumImage
{
    public string imageUrl { get; set; }

    public int height { get; set; }

    public int width { get; set; }

    public MediumImage ToMediumImage()
    {
        return new MediumImage
        {
            height = height,
            imageUrl = imageUrl,
            width = width
        };
    }
}