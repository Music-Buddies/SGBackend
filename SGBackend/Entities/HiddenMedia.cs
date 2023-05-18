using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

// only allow one entry per origin, per medium
[Index(nameof(UserId), nameof(HiddenMediumId), nameof(HiddenOrigin), IsUnique = true)]
public class HiddenMedia : BaseUserEntity
{
    public Guid HiddenMediumId { get; set; }
    
    public HiddenOrigin HiddenOrigin { get; set; }
}

public enum HiddenOrigin
{
    PersonalHistory,
    Discover
}