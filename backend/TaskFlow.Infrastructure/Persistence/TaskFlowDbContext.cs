using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options) : DbContext(options)
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BoardTask> Tasks => Set<BoardTask>();
    public DbSet<TaskStatusChange> TaskStatusChanges => Set<TaskStatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(board => board.Property(x => x.Name).HasMaxLength(120));

        modelBuilder.Entity<User>(user =>
        {
            user.Property(x => x.Email).HasMaxLength(254);
            user.Property(x => x.NormalizedEmail).HasMaxLength(254);
            user.Property(x => x.DisplayName).HasMaxLength(120);
            user.Property(x => x.PasswordHash).HasMaxLength(512);
            user.HasIndex(x => x.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<BoardMember>(member =>
        {
            member.HasKey(x => new { x.BoardId, x.UserId });
            member.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Restrict);
            member.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BoardTask>(task =>
        {
            task.ToTable("Tasks", table =>
            {
                table.HasCheckConstraint("CK_Tasks_Title_Length", "LEN(LTRIM(RTRIM([Title]))) BETWEEN 3 AND 120");
                table.HasCheckConstraint("CK_Tasks_Status", "[Status] IN ('Todo', 'InProgress', 'Done')");
                table.HasCheckConstraint("CK_Tasks_Priority", "[Priority] IN ('Low', 'Medium', 'High')");
                table.HasCheckConstraint("CK_Tasks_SortOrder", "[SortOrder] >= 0");
            });
            task.Property(x => x.Title).HasMaxLength(120);
            task.Property(x => x.Description).HasMaxLength(2000);
            task.Property(x => x.Priority).HasConversion<string>().HasMaxLength(10);
            task.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            task.Property(x => x.RowVersion).IsRowVersion();
            task.HasIndex(x => new { x.BoardId, x.ArchivedAt, x.Status, x.SortOrder, x.Id })
                .HasDatabaseName("IX_Tasks_Board_ActiveStatusOrder");

            task.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Restrict);
            task.HasOne<BoardMember>().WithMany().HasForeignKey(x => new { x.BoardId, x.AssigneeId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskStatusChange>(change =>
        {
            change.ToTable("TaskStatusChanges", table =>
            {
                table.HasCheckConstraint("CK_TaskStatusChanges_PreviousStatus", "[PreviousStatus] IN ('Todo', 'InProgress', 'Done')");
                table.HasCheckConstraint("CK_TaskStatusChanges_NewStatus", "[NewStatus] IN ('Todo', 'InProgress', 'Done')");
                table.HasCheckConstraint("CK_TaskStatusChanges_DifferentStatuses", "[PreviousStatus] <> [NewStatus]");
            });
            change.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(16);
            change.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(16);
            change.HasIndex(x => new { x.OccurredAtUtc, x.Id }).IsDescending(true, true);
            change.HasOne<BoardTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            change.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
