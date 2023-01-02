namespace SGBackend.Models;

public class MediaImage
{
    public Guid Id { get; set; }
    
    public string imageUrl { get; set; }
    
    public int height { get; set; }
    
    public int width { get; set; }
}