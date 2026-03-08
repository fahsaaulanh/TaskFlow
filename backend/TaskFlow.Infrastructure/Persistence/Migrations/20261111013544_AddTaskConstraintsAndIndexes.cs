using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskStatusChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatusChanges", x => x.Id);
                    table.CheckConstraint("CK_TaskStatusChanges_DifferentStatuses", "[PreviousStatus] <> [NewStatus]");
                    table.CheckConstraint("CK_TaskStatusChanges_NewStatus", "[NewStatus] IN ('Todo', 'InProgress', 'Done')");
                    table.CheckConstraint("CK_TaskStatusChanges_PreviousStatus", "[PreviousStatus] IN ('Todo', 'InProgress', 'Done')");
                    table.ForeignKey(
                        name: "FK_TaskStatusChanges_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskStatusChanges_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Board_ActiveStatusOrder",
                table: "Tasks",
                columns: new[] { "BoardId", "ArchivedAt", "Status", "SortOrder", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Priority",
                table: "Tasks",
                sql: "[Priority] IN ('Low', 'Medium', 'High')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_SortOrder",
                table: "Tasks",
                sql: "[SortOrder] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Status",
                table: "Tasks",
                sql: "[Status] IN ('Todo', 'InProgress', 'Done')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Title_Length",
                table: "Tasks",
                sql: "LEN(LTRIM(RTRIM([Title]))) BETWEEN 3 AND 120");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusChanges_ActorId",
                table: "TaskStatusChanges",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusChanges_OccurredAtUtc_Id",
                table: "TaskStatusChanges",
                columns: new[] { "OccurredAtUtc", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusChanges_TaskId",
                table: "TaskStatusChanges",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskStatusChanges");

            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Board_ActiveStatusOrder",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Priority",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_SortOrder",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Status",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Title_Length",
                table: "Tasks");
        }
    }
}
