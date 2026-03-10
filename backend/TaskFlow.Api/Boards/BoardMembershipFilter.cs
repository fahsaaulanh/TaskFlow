using TaskFlow.Application.Boards;

namespace TaskFlow.Api.Boards;

internal sealed class BoardMembershipFilter(IBoardQueries queries) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        if (!BoardEndpoints.TryGetUserId(context.User, out var userId)) return Results.Unauthorized();

        var routeValue = Convert.ToString(context.Request.RouteValues["boardId"], System.Globalization.CultureInfo.InvariantCulture);
        if (!int.TryParse(routeValue, out var boardId) ||
            !await queries.IsMemberAsync(boardId, userId, context.RequestAborted))
        {
            return Results.Problem(statusCode: 403, title: "You do not have access to this board.");
        }

        return await next(invocation);
    }
}
