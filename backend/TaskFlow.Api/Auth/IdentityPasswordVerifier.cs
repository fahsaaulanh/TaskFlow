using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Application.Auth;
using TaskFlow.Domain;

namespace TaskFlow.Api.Auth;

internal sealed class IdentityPasswordVerifier : IPasswordVerifier
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly User _unknownUser = new()
    {
        Email = string.Empty,
        NormalizedEmail = string.Empty,
        DisplayName = string.Empty,
        PasswordHash = string.Empty
    };

    public IdentityPasswordVerifier()
    {
        // Perform a hash verification even for an unrecognized email.
        _unknownUser.PasswordHash = _hasher.HashPassword(_unknownUser, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public bool Verify(User? user, string password)
    {
        var candidate = user ?? _unknownUser;
        var result = _hasher.VerifyHashedPassword(candidate, candidate.PasswordHash, password);
        return user is not null && result != PasswordVerificationResult.Failed;
    }
}
