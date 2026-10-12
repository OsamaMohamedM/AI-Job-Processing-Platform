using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _005_PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_AvailableAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "UX_JobResults_JobId",
                table: "JobResults");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Unpublished",
                table: "OutboxMessages",
                column: "AvailableAt",
                filter: "[PublishedAt] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Type", "Payload", "AttemptCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Outbox_Unpublished",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_AvailableAt",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "UX_JobResults_JobId",
                table: "JobResults",
                column: "JobId",
                unique: true);
        }
    }
}
