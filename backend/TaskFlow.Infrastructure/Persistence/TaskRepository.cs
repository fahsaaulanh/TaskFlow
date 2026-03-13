using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Boards;
using TaskFlow.Domain;
using TaskStatus = TaskFlow.Domain.TaskStatus;

namespace TaskFlow.Infrastructure.Persistence;

internal sealed class TaskRepository(TaskFlowDbContext db) : ITaskRepository
{
    public Task<BoardTask?> FindActiveAsync(int boardId, int taskId, CancellationToken cancellationToken) =>
        db.Tasks.SingleOrDefaultAsync(task => task.Id == taskId && task.BoardId == boardId && task.ArchivedAt == null,
            cancellationToken);

    public async Task<int> NextTodoOrderAsync(int boardId, CancellationToken cancellationToken)
    {
        var last = await db.Tasks.Where(task => task.BoardId == boardId && task.Status == TaskStatus.Todo && task.ArchivedAt == null)
            .MaxAsync(task => (int?)task.SortOrder, cancellationToken);
        return (last ?? -1) + 1;
    }

    public void Add(BoardTask task) => db.Tasks.Add(task);

    public Task<List<BoardTask>> ListDestinationAsync(int boardId, TaskStatus status, int movingTaskId,
        CancellationToken cancellationToken) =>
        db.Tasks.Where(task => task.BoardId == boardId && task.Status == status && task.Id != movingTaskId && task.ArchivedAt == null)
            .OrderBy(task => task.SortOrder).ThenBy(task => task.Id).ToListAsync(cancellationToken);
}

internal sealed class TaskFlowUnitOfWork(TaskFlowDbContext db) : IUnitOfWork
{
    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
