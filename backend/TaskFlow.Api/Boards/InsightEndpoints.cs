using TaskFlow.Application.Boards;

namespace TaskFlow.Api.Boards;

internal static class InsightEndpoints
{
    public static void MapInsights(this RouteGroupBuilder board)
    {
        board.MapGet("/analytics", async (int boardId, IInsightQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.GetAnalyticsAsync(boardId, cancellationToken)));
        board.MapGet("/activity", GetActivityAsync);
    }

    private static async Task<IResult> GetActivityAsync(int boardId, IInsightQueries queries, CancellationToken cancellationToken,
        DateOnly? from = null, DateOnly? to = null, int page = 1, int pageSize = 20)
    {
        var errors = new Dictionary<string, string[]>();
        if (from > to) errors["to"] = ["The end date must be on or after the start date."];
        if (page is < 1 or > 10000) errors["page"] = ["Choose a page from 1 to 10,000."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Choose a page size from 1 to 100."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var fromUtc = ToUtc(from);
        var toExclusiveUtc = to is { } end && end < DateOnly.MaxValue ? ToUtc(end.AddDays(1)) : null;
        return Results.Ok(await queries.GetActivityAsync(boardId, fromUtc, toExclusiveUtc, page, pageSize, cancellationToken));
    }

    private static DateTimeOffset? ToUtc(DateOnly? date) => date?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
