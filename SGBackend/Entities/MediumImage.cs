using SGBackend.Entities;

namespace SGBackend.Models;

public class MediumImage : BaseEntity
{
    public string imageUrl { get; set; }
    
    public int height { get; set; }
    
    public int width { get; set; }
}