namespace SGBackend.Entities;

public class State : BaseEntity
{
    public bool QuartzApplied { get; set; } = false;

    public bool GroupedFetchJobInstalled { get; set; } = false;
}