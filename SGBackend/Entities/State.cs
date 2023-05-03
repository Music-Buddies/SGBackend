namespace SGBackend.Entities;

public class State : BaseEntity
{
    public bool QuartzApplied { get; set; } = false;

    public bool GroupedFetchJobInstalled { get; set; } = false;

    public bool InitializedFromTarget { get; set; } = false;

    public bool RandomUsersGenerated { get; set; } = false;
}