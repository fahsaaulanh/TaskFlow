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
        tasks.MapPost("/", async (int boardId, TaskRequest request, TaskWriter writer, CancellationToken cancellationToken) =>
            ToResponse(await writer.CreateAsync(boardId, request.ToForm(), cancellationToken), StatusCodes.Status201Created));
        tasks.MapPut("/{taskId:int}", async (int boardId, int taskId, TaskRequest request, TaskWriter writer, CancellationToken cancellationToken) =>
            ToResponse(await writer.EditAsync(boardId, taskId, request.ToForm(), cancellationToken), StatusCodes.Status200OK));
        tasks.MapPost("/{taskId:int}/archive", async (int boardId, int taskId, ArchiveRequest request, TaskWriter writer,
            CancellationToken cancellationToken) =>
            ToResponse(await writer.ArchiveAsync(boardId, taskId, request.RowVersion, cancellationToken), StatusCodes.Status204NoContent));
        tasks.MapPost("/{taskId:int}/move", async (int boardId, int taskId, MoveRequest request, TaskWriter writer,
            ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            BoardEndpoints.TryGetUserId(principal, out var actorId)
                ? ToResponse(await writer.MoveAsync(boardId, taskId, request.ToMove(), actorId, cancellationToken), StatusCodes.Status200OK)
                : Results.Unauthorized());
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
