namespace TaskFlow.Domain;

public sealed class TaskStatusChange
{
    public long Id { get; init; }
    public int TaskId { get; init; }
    public int ActorId { get; init; }
    public TaskStatus PreviousStatus { get; init; }
    public TaskStatus NewStatus { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
}
