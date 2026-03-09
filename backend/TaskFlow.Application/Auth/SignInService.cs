using TaskFlow.Domain;

namespace TaskFlow.Application.Auth;

public sealed class SignInService(IUserRepository users, IPasswordVerifier passwords)
{
    public async Task<SignedInUser?> VerifyAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByNormalizedEmailAsync(email.Trim().ToUpperInvariant(), cancellationToken);
        if (!passwords.Verify(user, password) || user is null)
        {
            return null;
        }

        return new SignedInUser(user.Id, user.Email, user.DisplayName);
    }
}

public sealed record SignedInUser(int Id, string Email, string DisplayName);

public interface IUserRepository
{
    Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
}

public interface IPasswordVerifier
{
    bool Verify(User? user, string password);
}
