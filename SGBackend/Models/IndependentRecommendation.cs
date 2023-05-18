using SGBackend.Entities;

namespace SGBackend.Models;

public class IndependentRecommendation
{
    
    public string mediumId { get; set; }
    
    public long orderValue { get; set; }
    
    public bool hidden { get; set; }

    public string username { get; set; }

    public string profileUrl { get; set; }

    public long listenedSecondsMatch { get; set; }

    public MediumImage[] albumImages { get; set; }

    public string[] allArtists { get; set; }

    public string linkToMedia { get; set; }

    public string albumName { get; set; }

    public bool explicitFlag { get; set; }

    public string songTitle { get; set; }
}