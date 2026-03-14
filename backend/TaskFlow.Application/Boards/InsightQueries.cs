namespace TaskFlow.Application.Boards;

public interface IInsightQueries
{
    Task<BoardAnalytics> GetAnalyticsAsync(int boardId, CancellationToken cancellationToken);
    Task<ActivityPage> GetActivityAsync(int boardId, DateTimeOffset? fromUtc, DateTimeOffset? toExclusiveUtc,
        int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record BoardAnalytics(int Total, int Todo, int InProgress, int Done, int Overdue);
public sealed record ActivityEntry(long Id, int TaskId, string TaskTitle, string ActorName,
    string PreviousStatus, string NewStatus, DateTimeOffset OccurredAtUtc);
public sealed record ActivityPage(IReadOnlyList<ActivityEntry> Items, int Page, int PageSize, bool HasMore);
