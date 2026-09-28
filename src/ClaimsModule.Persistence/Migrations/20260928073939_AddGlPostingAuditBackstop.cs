using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlPostingAuditBackstop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_ClaimAuditLog_RelatedEntityId_GlPostingSimulated",
                table: "ClaimAuditLog",
                column: "RelatedEntityId",
                unique: true,
                filter: "[EventType] = N'GL_POSTING_SIMULATED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ClaimAuditLog_RelatedEntityId_GlPostingSimulated",
                table: "ClaimAuditLog");
        }
    }
}
