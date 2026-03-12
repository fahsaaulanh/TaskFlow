using System.Globalization;
using System.Security.Claims;
using TaskFlow.Application.Boards;
using TaskFlow.Domain;
using TaskStatus = TaskFlow.Domain.TaskStatus;

namespace TaskFlow.Api.Boards;

internal static class BoardEndpoints
{
    public static void MapTaskFlowBoards(this IEndpointRouteBuilder endpoints)
    {
        var boards = endpoints.MapGroup("/api/boards").RequireAuthorization();
        boards.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(invocation);
        });

        boards.MapGet("/", async (ClaimsPrincipal principal, IBoardQueries queries, CancellationToken cancellationToken) =>
            TryGetUserId(principal, out var userId)
                ? Results.Ok(await queries.ListForUserAsync(userId, cancellationToken))
                : Results.Unauthorized());

        // Mount all board-scoped endpoints under this group so they share the membership check.
        var board = boards.MapGroup("/{boardId:int}").AddEndpointFilter<BoardMembershipFilter>();
        board.MapGet("/members", async (int boardId, IBoardQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.ListMembersAsync(boardId, cancellationToken)));
        board.MapGet("/tasks", ListTasksAsync);
        board.MapTaskWrites();
    }

    internal static bool TryGetUserId(ClaimsPrincipal principal, out int userId) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out userId);

    private static async Task<IResult> ListTasksAsync(int boardId, string? status, string? priority, int? assigneeId,
        IBoardQueries queries, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var taskStatus = ParseFilter<TaskStatus>(status, "status", errors);
        var taskPriority = ParseFilter<TaskPriority>(priority, "priority", errors);
        if (assigneeId is <= 0) errors["assigneeId"] = ["Choose a valid assignee."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        return Results.Ok(await queries.ListTasksAsync(boardId, new(taskStatus, taskPriority, assigneeId), cancellationToken));
    }

    private static T? ParseFilter<T>(string? value, string field, Dictionary<string, string[]> errors) where T : struct, Enum
    {
        if (value is null) return null;
        var name = Array.Find(Enum.GetNames<T>(), name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (name is not null) return Enum.Parse<T>(name);
        errors[field] = [$"Choose one of: {string.Join(", ", Enum.GetNames<T>())}."];
        return null;
    }
}
