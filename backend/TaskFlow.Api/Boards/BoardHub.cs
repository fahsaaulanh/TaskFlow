using Microsoft.AspNetCore.SignalR;
using TaskFlow.Application.Boards;

namespace TaskFlow.Api.Boards;

public interface IBoardClient
{
    Task BoardChanged();
}

public sealed class BoardHub(IBoardQueries queries) : Hub<IBoardClient>
{
    public async Task WatchBoard(int boardId)
    {
        if (Context.User is not { } user || !BoardEndpoints.TryGetUserId(user, out var userId) ||
            !await queries.IsMemberAsync(boardId, userId, Context.ConnectionAborted))
        {
            throw new HubException("You do not have access to this board.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId), Context.ConnectionAborted);
    }

    internal static string GroupName(int boardId) => FormattableString.Invariant($"board:{boardId}");

    internal static bool IsSameOrigin(HttpRequest request)
    {
        var origins = request.Headers.Origin;
        if (origins.Count == 0) return true;

        return origins.Count == 1 && Uri.TryCreate(origins[0], UriKind.Absolute, out var origin) &&
            Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var server) &&
            string.Equals(origin.GetLeftPart(UriPartial.Authority), server.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class BoardChangeNotifier(IHubContext<BoardHub, IBoardClient> hub, ILogger<BoardChangeNotifier> logger)
{
    public async Task NotifyAsync(int boardId)
    {
        try
        {
            await hub.Clients.Group(BoardHub.GroupName(boardId)).BoardChanged();
        }
        catch (Exception error)
        {
            // The database write has already committed. Report the delivery failure without failing the write.
            logger.LogWarning(error, "Could not broadcast a committed change for board {BoardId}.", boardId);
        }
    }
}
