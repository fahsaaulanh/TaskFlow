namespace TaskFlow.Domain;

public sealed class BoardTask
{
    public int Id { get; init; }
    public int BoardId { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? AssigneeId { get; set; }
    public TaskStatus Status { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public byte[] RowVersion { get; private set; } = [];
}

public enum TaskPriority { Low, Medium, High }

public enum TaskStatus { Todo, InProgress, Done }
