using TaskFlow.Domain;
using TaskStatus = TaskFlow.Domain.TaskStatus;

namespace TaskFlow.Application.Boards;

public sealed record TaskForm(string? Title, string? Description, TaskPriority? Priority,
    DateOnly? DueDate, int? AssigneeId, byte[]? RowVersion = null);

public enum TaskWriteStatus { Saved, Invalid, NotFound, Conflict }

public sealed record TaskWriteResult(TaskWriteStatus Status, TaskSummary? Task = null,
    Dictionary<string, string[]>? Errors = null);

public interface ITaskRepository
{
    Task<BoardTask?> FindActiveAsync(int boardId, int taskId, CancellationToken cancellationToken);
    Task<int> NextTodoOrderAsync(int boardId, CancellationToken cancellationToken);
    void Add(BoardTask task);
}

public interface IUnitOfWork
{
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}

public sealed class TaskWriter(ITaskRepository tasks, IBoardQueries boards, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<TaskWriteResult> CreateAsync(int boardId, TaskForm form, CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(boardId, form, cancellationToken);
        if (errors.Count > 0) return new(TaskWriteStatus.Invalid, Errors: errors);

        var now = clock.GetUtcNow();
        var task = new BoardTask
        {
            BoardId = boardId,
            Title = form.Title!.Trim(),
            Status = TaskStatus.Todo,
            SortOrder = await tasks.NextTodoOrderAsync(boardId, cancellationToken),
            CreatedAt = now
        };
        ApplyForm(task, form, now);
        tasks.Add(task);
        return await SaveAsync(task, cancellationToken);
    }

    public async Task<TaskWriteResult> EditAsync(int boardId, int taskId, TaskForm form, CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(boardId, form, cancellationToken);
        if (form.RowVersion is not { Length: 8 }) errors["rowVersion"] = ["A valid task version is required."];
        if (errors.Count > 0) return new(TaskWriteStatus.Invalid, Errors: errors);

        var task = await tasks.FindActiveAsync(boardId, taskId, cancellationToken);
        if (task is null) return new(TaskWriteStatus.NotFound);
        if (!task.RowVersion.AsSpan().SequenceEqual(form.RowVersion)) return new(TaskWriteStatus.Conflict);

        ApplyForm(task, form, clock.GetUtcNow());
        return await SaveAsync(task, cancellationToken);
    }

    public async Task<TaskWriteResult> ArchiveAsync(int boardId, int taskId, byte[]? rowVersion, CancellationToken cancellationToken)
    {
        if (rowVersion is not { Length: 8 })
        {
            return new(TaskWriteStatus.Invalid, Errors: new() { ["rowVersion"] = ["A valid task version is required."] });
        }

        var task = await tasks.FindActiveAsync(boardId, taskId, cancellationToken);
        if (task is null) return new(TaskWriteStatus.NotFound);
        if (!task.RowVersion.AsSpan().SequenceEqual(rowVersion)) return new(TaskWriteStatus.Conflict);

        task.ArchivedAt = task.UpdatedAt = clock.GetUtcNow();
        return await SaveAsync(task, cancellationToken);
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(int boardId, TaskForm form, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (form.Title?.Trim().Length is not (>= 3 and <= 120))
            errors["title"] = ["Enter a title from 3 to 120 characters."];
        if (form.Description?.Length > 2000)
            errors["description"] = ["Enter a description of at most 2,000 characters."];
        if (form.Priority is not { } priority || !Enum.IsDefined(priority))
            errors["priority"] = ["Choose a valid priority."];
        if (form.AssigneeId is { } assigneeId && (assigneeId <= 0 || !await boards.IsMemberAsync(boardId, assigneeId, cancellationToken)))
            errors["assigneeId"] = ["Choose a member of this board."];
        return errors;
    }

    private static void ApplyForm(BoardTask task, TaskForm form, DateTimeOffset updatedAt)
    {
        task.Title = form.Title!.Trim();
        task.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        task.Priority = form.Priority!.Value;
        task.DueDate = form.DueDate;
        task.AssigneeId = form.AssigneeId;
        task.UpdatedAt = updatedAt;
    }

    private async Task<TaskWriteResult> SaveAsync(BoardTask task, CancellationToken cancellationToken)
    {
        if (!await unitOfWork.TrySaveAsync(cancellationToken)) return new(TaskWriteStatus.Conflict);

        return new(TaskWriteStatus.Saved, new TaskSummary(task.Id, task.Title, task.Description, task.Status,
            task.Priority, task.DueDate, task.AssigneeId, task.SortOrder, task.RowVersion));
    }
}
