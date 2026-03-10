using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Boards;

namespace TaskFlow.Infrastructure.Persistence;

internal sealed class BoardQueries(TaskFlowDbContext db) : IBoardQueries
{
    public async Task<IReadOnlyList<BoardSummary>> ListForUserAsync(int userId, CancellationToken cancellationToken) =>
        await (from membership in db.BoardMembers
               join board in db.Boards on membership.BoardId equals board.Id
               where membership.UserId == userId
               orderby board.Id
               select new BoardSummary(board.Id, board.Name)).ToListAsync(cancellationToken);

    public Task<bool> IsMemberAsync(int boardId, int userId, CancellationToken cancellationToken) =>
        db.BoardMembers.AnyAsync(member => member.BoardId == boardId && member.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<BoardMemberSummary>> ListMembersAsync(int boardId, CancellationToken cancellationToken) =>
        await (from membership in db.BoardMembers
               join user in db.Users on membership.UserId equals user.Id
               where membership.BoardId == boardId
               orderby user.DisplayName, user.Id
               select new BoardMemberSummary(user.Id, user.DisplayName)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskSummary>> ListTasksAsync(int boardId, TaskFilters filters, CancellationToken cancellationToken)
    {
        var tasks = db.Tasks.Where(task => task.BoardId == boardId && task.ArchivedAt == null);
        if (filters.Status is { } status) tasks = tasks.Where(task => task.Status == status);
        if (filters.Priority is { } priority) tasks = tasks.Where(task => task.Priority == priority);
        if (filters.AssigneeId is { } assigneeId) tasks = tasks.Where(task => task.AssigneeId == assigneeId);

        return await tasks.OrderBy(task => task.Status).ThenBy(task => task.SortOrder).ThenBy(task => task.Id)
            .Select(task => new TaskSummary(task.Id, task.Title, task.Description, task.Status, task.Priority,
                task.DueDate, task.AssigneeId, task.SortOrder, task.RowVersion))
            .ToListAsync(cancellationToken);
    }
}
