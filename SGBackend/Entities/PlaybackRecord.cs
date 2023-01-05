using SGBackend.Entities;

namespace SGBackend.Models;


// defines a singular record of a medium being played
public class PlaybackRecord : BaseUserEntity
{
    public Medium Medium { get; set; }

    public DateTime PlayedAt { get; set; }
    
    public int PlayedSeconds { get; set; }
}