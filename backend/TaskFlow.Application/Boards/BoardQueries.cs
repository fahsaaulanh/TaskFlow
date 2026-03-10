using TaskFlow.Domain;
using TaskStatus = TaskFlow.Domain.TaskStatus;

namespace TaskFlow.Application.Boards;

public interface IBoardQueries
{
    Task<IReadOnlyList<BoardSummary>> ListForUserAsync(int userId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(int boardId, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BoardMemberSummary>> ListMembersAsync(int boardId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskSummary>> ListTasksAsync(int boardId, TaskFilters filters, CancellationToken cancellationToken);
}

public sealed record BoardSummary(int Id, string Name);
public sealed record BoardMemberSummary(int Id, string DisplayName);
public sealed record TaskFilters(TaskStatus? Status, TaskPriority? Priority, int? AssigneeId);
public sealed record TaskSummary(int Id, string Title, string? Description, TaskStatus Status, TaskPriority Priority,
    DateOnly? DueDate, int? AssigneeId, int SortOrder, byte[] RowVersion);
