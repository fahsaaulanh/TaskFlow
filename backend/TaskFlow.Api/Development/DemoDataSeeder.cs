using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain;
using TaskFlow.Infrastructure.Persistence;
using TaskStatus = TaskFlow.Domain.TaskStatus;

namespace TaskFlow.Api.Development;

internal static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.Boards.AnyAsync() || await db.Users.AnyAsync())
        {
            logger.LogInformation("Demo seed skipped because the database already contains boards or users.");
            return;
        }

        var password = configuration["TaskFlow:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException("Set TaskFlow__DemoPassword to a local password of at least 12 characters.");
        }

        var hasher = new PasswordHasher<User>();
        var alex = CreateUser("alex@taskflow.test", "Alex", password, hasher);
        var dina = CreateUser("dina@taskflow.test", "Dina", password, hasher);
        var board = new Board { Name = "Team board" };
        db.Boards.Add(board);
        db.Users.AddRange(alex, dina);
        await db.SaveChangesAsync();

        db.BoardMembers.AddRange(
            new BoardMember { BoardId = board.Id, UserId = alex.Id },
            new BoardMember { BoardId = board.Id, UserId = dina.Id });

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        db.Tasks.AddRange(
            new BoardTask
            {
                BoardId = board.Id,
                Title = "Review the demo checklist",
                Description = "Check the sign-in, board, and insights flows before the demo.",
                Priority = TaskPriority.Medium,
                Status = TaskStatus.Todo,
                DueDate = today.AddDays(2),
                CreatedAt = now,
                UpdatedAt = now
            },
            new BoardTask
            {
                BoardId = board.Id,
                Title = "Write the local setup guide",
                Priority = TaskPriority.High,
                Status = TaskStatus.InProgress,
                AssigneeId = dina.Id,
                DueDate = today.AddDays(-1),
                CreatedAt = now,
                UpdatedAt = now
            },
            new BoardTask
            {
                BoardId = board.Id,
                Title = "Create the TaskFlow workspace",
                Priority = TaskPriority.Low,
                Status = TaskStatus.Done,
                AssigneeId = alex.Id,
                CreatedAt = now,
                UpdatedAt = now
            });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Seeded the demo board, two users, and three tasks.");
    }

    private static User CreateUser(string email, string displayName, string password, PasswordHasher<User> hasher)
    {
        var user = new User
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = displayName,
            PasswordHash = string.Empty
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
}
