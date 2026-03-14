using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Boards;

namespace TaskFlow.Infrastructure.Persistence;

internal sealed class InsightQueries(TaskFlowDbContext db) : IInsightQueries
{
    public async Task<BoardAnalytics> GetAnalyticsAsync(int boardId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<BoardAnalytics>($"""
            SELECT Total, Todo, InProgress, Done, Overdue
            FROM dbo.BoardAnalytics WHERE BoardId = {boardId}
            """).SingleOrDefaultAsync(cancellationToken) ?? new(0, 0, 0, 0, 0);

    public async Task<ActivityPage> GetActivityAsync(int boardId, DateTimeOffset? fromUtc, DateTimeOffset? toExclusiveUtc,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        var rows = await db.Database.SqlQuery<ActivityEntry>($"""
            EXEC dbo.GetBoardActivity @BoardId = {boardId}, @FromUtc = {fromUtc}, @ToExclusiveUtc = {toExclusiveUtc},
                @Offset = {offset}, @Take = {pageSize + 1}
            """).ToListAsync(cancellationToken);
        return new(rows.Take(pageSize).ToArray(), page, pageSize, rows.Count > pageSize);
    }
}
