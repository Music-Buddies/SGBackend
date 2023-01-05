using System.Net.Mime;
using SGBackend.Models;

namespace SGBackend.Controllers;

public class MediaSummary
{
    public string songTitle { get; set; }
    
    public string[] allArtists { get; set; }
    
    public bool explicitFlag { get; set; }
    
    public long listenedSeconds { get; set; }
    
    public MediumImage[] albumImages { get; set; }
    
    public string linkToMedia { get; set; }
}