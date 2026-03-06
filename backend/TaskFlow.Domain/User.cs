namespace TaskFlow.Domain;

public sealed class User
{
    public int Id { get; init; }
    public required string Email { get; init; }
    public required string NormalizedEmail { get; init; }
    public required string DisplayName { get; init; }
    public required string PasswordHash { get; set; }
}
