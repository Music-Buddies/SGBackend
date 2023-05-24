using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

// only allow one entry per origin, per medium
[Index(nameof(UserId), nameof(MediumId), nameof(HiddenOrigin), IsUnique = true)]
public class HiddenMedia : BaseUserEntity
{
    public Guid MediumId { get; set; }
    
    public Medium Medium { get; set; }
    
    public HiddenOrigin HiddenOrigin { get; set; }
}

public enum HiddenOrigin
{
    PersonalHistory,
    Discover
}