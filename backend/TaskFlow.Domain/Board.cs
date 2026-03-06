namespace TaskFlow.Domain;

public sealed class Board
{
    public int Id { get; init; }
    public required string Name { get; init; }
}

public sealed class BoardMember
{
    public int BoardId { get; init; }
    public int UserId { get; init; }
}
