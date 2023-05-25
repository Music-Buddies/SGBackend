namespace SGBackend.Entities;

/// <summary>
/// Used by the state manager to determine actions that need to be run once on create of the database / instance.
/// </summary>
public class State : BaseEntity
{
    public bool QuartzApplied { get; set; } = false;

    public bool GroupedFetchJobInstalled { get; set; } = false;

    public bool InitializedFromTarget { get; set; } = false;

    public bool RandomUsersGenerated { get; set; } = false;
}