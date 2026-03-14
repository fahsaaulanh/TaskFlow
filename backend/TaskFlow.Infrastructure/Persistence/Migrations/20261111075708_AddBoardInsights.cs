using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW dbo.BoardAnalytics AS
                SELECT b.Id AS BoardId,
                       COUNT(t.Id) AS Total,
                       COUNT(CASE WHEN t.Status = N'Todo' THEN 1 END) AS Todo,
                       COUNT(CASE WHEN t.Status = N'InProgress' THEN 1 END) AS InProgress,
                       COUNT(CASE WHEN t.Status = N'Done' THEN 1 END) AS Done,
                       COUNT(CASE WHEN t.Status <> N'Done' AND t.DueDate < CONVERT(date, SYSUTCDATETIME()) THEN 1 END) AS Overdue
                FROM dbo.Boards AS b
                LEFT JOIN dbo.Tasks AS t ON t.BoardId = b.Id AND t.ArchivedAt IS NULL
                GROUP BY b.Id;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.RecordTaskStatusChange ON dbo.Tasks AFTER UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF NOT UPDATE(Status) RETURN;

                    IF NOT EXISTS (SELECT 1 FROM inserted AS i JOIN deleted AS d ON d.Id = i.Id WHERE i.Status <> d.Status)
                        RETURN;

                    DECLARE @ActorId int = TRY_CONVERT(int, SESSION_CONTEXT(N'TaskFlowActorId'));
                    IF @ActorId IS NULL OR EXISTS (
                        SELECT 1 FROM inserted AS i JOIN deleted AS d ON d.Id = i.Id
                        WHERE i.Status <> d.Status AND NOT EXISTS (
                            SELECT 1 FROM dbo.BoardMembers AS m WHERE m.BoardId = i.BoardId AND m.UserId = @ActorId
                        )
                    )
                        THROW 51001, 'A board member must be identified for a task status change.', 1;

                    INSERT dbo.TaskStatusChanges (TaskId, ActorId, PreviousStatus, NewStatus, OccurredAtUtc)
                    SELECT i.Id, @ActorId, d.Status, i.Status, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
                    FROM inserted AS i JOIN deleted AS d ON d.Id = i.Id
                    WHERE i.Status <> d.Status;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.ProtectTaskStatusHistory ON dbo.TaskStatusChanges INSTEAD OF UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51002, 'Task status history cannot be edited or deleted.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE PROCEDURE dbo.GetBoardActivity
                    @BoardId int,
                    @FromUtc datetimeoffset(7),
                    @ToExclusiveUtc datetimeoffset(7),
                    @Offset int,
                    @Take int
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT h.Id, h.TaskId, t.Title AS TaskTitle, u.DisplayName AS ActorName,
                           h.PreviousStatus, h.NewStatus, h.OccurredAtUtc
                    FROM dbo.TaskStatusChanges AS h
                    JOIN dbo.Tasks AS t ON t.Id = h.TaskId
                    JOIN dbo.Users AS u ON u.Id = h.ActorId
                    WHERE t.BoardId = @BoardId
                      AND (@FromUtc IS NULL OR h.OccurredAtUtc >= @FromUtc)
                      AND (@ToExclusiveUtc IS NULL OR h.OccurredAtUtc < @ToExclusiveUtc)
                    ORDER BY h.OccurredAtUtc DESC, h.Id DESC
                    OFFSET @Offset ROWS FETCH NEXT @Take ROWS ONLY;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE dbo.GetBoardActivity;");
            migrationBuilder.Sql("DROP TRIGGER dbo.ProtectTaskStatusHistory;");
            migrationBuilder.Sql("DROP TRIGGER dbo.RecordTaskStatusChange;");
            migrationBuilder.Sql("DROP VIEW dbo.BoardAnalytics;");
        }
    }
}
