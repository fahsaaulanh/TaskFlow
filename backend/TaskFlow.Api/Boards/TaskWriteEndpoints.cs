using System.Security.Claims;
using TaskFlow.Api.Auth;
using TaskFlow.Application.Boards;
using TaskFlow.Domain;
using TaskStatus = TaskFlow.Domain.TaskStatus;

namespace TaskFlow.Api.Boards;

internal static class TaskWriteEndpoints
{
    public static void MapTaskWrites(this RouteGroupBuilder board)
    {
        var tasks = board.MapGroup("/tasks").AddEndpointFilter<AntiforgeryFilter>();
        tasks.MapPost("/", async (int boardId, TaskRequest request, TaskWriter writer, BoardChangeNotifier notifier,
            CancellationToken cancellationToken) =>
            await CompleteAsync(boardId, await writer.CreateAsync(boardId, request.ToForm(), cancellationToken),
                StatusCodes.Status201Created, notifier));
        tasks.MapPut("/{taskId:int}", async (int boardId, int taskId, TaskRequest request, TaskWriter writer,
            BoardChangeNotifier notifier, CancellationToken cancellationToken) =>
            await CompleteAsync(boardId, await writer.EditAsync(boardId, taskId, request.ToForm(), cancellationToken),
                StatusCodes.Status200OK, notifier));
        tasks.MapPost("/{taskId:int}/archive", async (int boardId, int taskId, ArchiveRequest request, TaskWriter writer,
            BoardChangeNotifier notifier, CancellationToken cancellationToken) =>
            await CompleteAsync(boardId, await writer.ArchiveAsync(boardId, taskId, request.RowVersion, cancellationToken),
                StatusCodes.Status204NoContent, notifier));
        tasks.MapPost("/{taskId:int}/move", async (int boardId, int taskId, MoveRequest request, TaskWriter writer,
            ClaimsPrincipal principal, BoardChangeNotifier notifier, CancellationToken cancellationToken) =>
            BoardEndpoints.TryGetUserId(principal, out var actorId)
                ? await CompleteAsync(boardId, await writer.MoveAsync(boardId, taskId, request.ToMove(), actorId, cancellationToken),
                    StatusCodes.Status200OK, notifier)
                : Results.Unauthorized());
    }

    private static async Task<IResult> CompleteAsync(int boardId, TaskWriteResult result, int successStatus, BoardChangeNotifier notifier)
    {
        if (result.Status == TaskWriteStatus.Saved)
        {
            await notifier.NotifyAsync(boardId);
        }

        return ToResponse(result, successStatus);
    }

    private static IResult ToResponse(TaskWriteResult result, int successStatus) => result.Status switch
    {
        TaskWriteStatus.Saved when successStatus == StatusCodes.Status204NoContent => Results.NoContent(),
        TaskWriteStatus.Saved => Results.Json(result.Task, statusCode: successStatus),
        TaskWriteStatus.Invalid => Results.ValidationProblem(result.Errors!),
        TaskWriteStatus.NotFound => Results.Problem(statusCode: 404, title: "Task not found."),
        TaskWriteStatus.Conflict => Results.Problem(statusCode: 409, title: "This task has changed. Reopen it and try again."),
        _ => throw new InvalidOperationException("Unknown task write result.")
    };

    private sealed record ArchiveRequest(byte[]? RowVersion);

    private sealed record MoveRequest(string? Status, int? BeforeTaskId, byte[]? RowVersion)
    {
        public TaskMove ToMove() => new(Status switch
        {
            "Todo" => TaskStatus.Todo,
            "InProgress" => TaskStatus.InProgress,
            "Done" => TaskStatus.Done,
            _ => null
        }, BeforeTaskId, RowVersion);
    }

    private sealed record TaskRequest(string? Title, string? Description, string? Priority,
        DateOnly? DueDate, int? AssigneeId, byte[]? RowVersion)
    {
        public TaskForm ToForm() => new(Title, Description, Priority switch
        {
            "Low" => TaskPriority.Low,
            "Medium" => TaskPriority.Medium,
            "High" => TaskPriority.High,
            _ => null
        }, DueDate, AssigneeId, RowVersion);
    }
}
