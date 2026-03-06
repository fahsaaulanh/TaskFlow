using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options) : DbContext(options)
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BoardTask> Tasks => Set<BoardTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(board => board.Property(x => x.Name).HasMaxLength(120));

        modelBuilder.Entity<User>(user =>
        {
            user.Property(x => x.Email).HasMaxLength(254);
            user.Property(x => x.NormalizedEmail).HasMaxLength(254);
            user.Property(x => x.DisplayName).HasMaxLength(120);
            user.Property(x => x.PasswordHash).HasMaxLength(512);
        });

        modelBuilder.Entity<BoardMember>(member =>
        {
            member.HasKey(x => new { x.BoardId, x.UserId });
            member.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Restrict);
            member.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BoardTask>(task =>
        {
            task.Property(x => x.Title).HasMaxLength(120);
            task.Property(x => x.Description).HasMaxLength(2000);
            task.Property(x => x.Priority).HasConversion<string>().HasMaxLength(10);
            task.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            task.Property(x => x.RowVersion).IsRowVersion();

            task.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Restrict);
            task.HasOne<BoardMember>().WithMany().HasForeignKey(x => new { x.BoardId, x.AssigneeId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
