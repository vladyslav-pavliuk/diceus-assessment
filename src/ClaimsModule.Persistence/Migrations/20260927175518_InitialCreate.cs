using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Organisations",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Name", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Demo Insurance Company", null, null, null });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "DisplayName", "IsActive", "OrganisationId", "Role", "UpdatedAt", "UserCreated", "UserModified", "Username" },
                values: new object[,]
                {
                    { new Guid("3a97300c-8566-47a8-8e24-87ce3dae8759"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Finley Hayes", true, new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Manager", null, null, null, "manager.finley" },
                    { new Guid("415b998a-b77b-4d44-9a25-8f506dfd17e4"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Alex Carter", true, new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Handler", null, null, null, "handler.alex" },
                    { new Guid("6b870c3c-4333-441b-8c57-88f277f61fca"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Blake Jordan", true, new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Handler", null, null, null, "handler.blake" },
                    { new Guid("7b2e441e-dc0b-42fc-8a8d-0b7e1308e1b8"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Casey Morgan", true, new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Supervisor", null, null, null, "supervisor.casey" },
                    { new Guid("c65a96eb-f89d-4a31-be62-eb79ea9343cf"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Emery Brooks", true, new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Manager", null, null, null, "manager.emery" },
                    { new Guid("cbbca2e1-4bd9-4714-9f79-735500e13c5d"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Drew Taylor", true, new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Supervisor", null, null, null, "supervisor.drew" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganisationId",
                table: "Users",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Organisations");
        }
    }
}
