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
                name: "ClaimStatusTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FromStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinimumRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RequiresReason = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSystemOnly = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusTransitions", x => x.Id);
                });

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
                name: "CauseOfLossCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PerilCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauseOfLossCodes", x => x.Id);
                    table.UniqueConstraint("AK_CauseOfLossCodes_OrganisationId_Code", x => new { x.OrganisationId, x.Code });
                    table.ForeignKey(
                        name: "FK_CauseOfLossCodes_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimNumberCounters",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimNumberCounters", x => new { x.OrganisationId, x.Year });
                    table.CheckConstraint("CK_ClaimNumberCounters_LastValue", "[LastValue] BETWEEN 1 AND 9999999");
                    table.ForeignKey(
                        name: "FK_ClaimNumberCounters_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CoverageTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Policies_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportedDate = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    AssignedHandlerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReserveLimitOverride = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReserveLimitOverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReserveLimitOverrideByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReserveLimitOverrideAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Users_AssignedHandlerId",
                        column: x => x.AssignedHandlerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimAuditLog_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimDocuments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimParties_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimReserveComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LastChangeSequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimReserveComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimReserveComponents_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRiskObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DamageDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AssetReference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRiskObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimRiskObjects_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimValidationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimValidationIssues_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LossEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LossDate = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    LossDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LossLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CauseOfLossCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EstimatedLossAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ReportDate = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    PoliceReportNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LossEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LossEvents_CauseOfLossCodes_OrganisationId_CauseOfLossCode",
                        columns: x => new { x.OrganisationId, x.CauseOfLossCode },
                        principalTable: "CauseOfLossCodes",
                        principalColumns: new[] { "OrganisationId", "Code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LossEvents_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReserveHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ReserveComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredAuthority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExceedsAggregateLimit = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PostingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostingJobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangeSequence = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    UserCreated = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserModified = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReserveHistory_ClaimReserveComponents_ReserveComponentId",
                        column: x => x.ReserveComponentId,
                        principalTable: "ClaimReserveComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReserveHistory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("2543c201-57c1-4f59-bb00-4a7fcc32f36a"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", "Handler", "Open", null, null, null });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "RequiresReason", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("2849f2d3-700e-4713-92ad-8f12a457b94c"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", "Handler", true, "Closed", null, null, null },
                    { new Guid("28fbe2ff-696e-4100-a73e-66465a356dd1"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Closed", "Supervisor", true, "Reopened", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("38a97a48-271f-4214-9db7-024d2e245127"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", "Handler", "Open", null, null, null });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "RequiresReason", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("566f5ccb-82dd-4b28-a2bb-9210ce42b047"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "PendingPayment", "Handler", true, "Closed", null, null, null });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("761b6cc3-850c-40d7-980e-a6bc8614946a"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", "Handler", "PendingPayment", null, null, null });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "IsSystemOnly", "MinimumRole", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("8cad141b-381d-4ffd-918c-83306c42a6d3"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", true, null, "Open", null, null, null });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "RequiresReason", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("b415c0c6-5358-44e1-9bad-f1d2ff3a43f3"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", "Handler", true, "Closed", null, null, null });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("b99eafea-c240-4a77-bd3f-7c7e4ad40679"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", "Handler", "UnderInvestigation", null, null, null },
                    { new Guid("c030fc17-dcb5-4848-9253-3364e3a7807e"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", "Handler", "PendingPayment", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "MinimumRole", "RequiresReason", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("f7ff1102-bd41-4064-b11f-778d0181f00a"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", "Handler", true, "Withdrawn", null, null, null },
                    { new Guid("ffe17aee-588f-469e-bedb-f4ea8903782f"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", "Handler", true, "Withdrawn", null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Organisations",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Name", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Demo Insurance Company", null, null, null });

            migrationBuilder.InsertData(
                table: "CauseOfLossCodes",
                columns: new[] { "Id", "Code", "CreatedAt", "DeletedAt", "IsActive", "Name", "Notes", "OrganisationId", "PerilCategory", "SortOrder", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("1a56ed19-8a6f-40f7-9bd4-f0e30d94b90f"), "COL-FLOOD", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Flood", "Water intrusion from external source", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Weather", 20, null, null, null },
                    { new Guid("3a6d297a-8cef-4352-9125-a7067f9244c0"), "COL-EQUIP", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Equipment Breakdown", "Mechanical or electrical failure", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Equipment", 70, null, null, null },
                    { new Guid("4091c278-fbd1-4744-964e-910c77ba8430"), "COL-VEH-COL", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Vehicle Collision", "Impact with another vehicle or object", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Auto", 40, null, null, null },
                    { new Guid("84395762-ac14-4e68-bdad-afe906824b9b"), "COL-VEH-COMP", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Vehicle Comprehensive", "Weather, theft, vandalism", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Auto", 50, null, null, null },
                    { new Guid("8d4d2ac8-35d2-4a3c-b034-1a690f2f9641"), "COL-LIAB", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Third Party Liability", "Bodily injury or property damage to third party", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Liability", 60, null, null, null },
                    { new Guid("98e9408f-6ee8-47b5-8319-3a240c46d516"), "COL-INJURY", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Bodily Injury", "Personal injury to claimant", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Liability", 90, null, null, null },
                    { new Guid("997a4c70-15b9-49b4-9b50-210df0527a32"), "COL-FIRE", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Fire", "Structure or contents fire", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Property", 10, null, null, null },
                    { new Guid("997fa883-2560-4c33-8fb4-53aec3583554"), "COL-WIND", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Wind / Storm", "Wind, hail, hurricane", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Weather", 80, null, null, null },
                    { new Guid("e597f02e-2d9f-4687-91b4-c7a02bcc4ef3"), "COL-OTHER", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Other / Unknown", "Catch-all for uncategorised losses", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "General", 100, null, null, null },
                    { new Guid("f6a7ba5c-0632-4907-bdc7-3d05d5705ee4"), "COL-THEFT", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Theft", "Burglary, robbery, larceny", new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "Crime", 30, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Policies",
                columns: new[] { "Id", "ClientName", "CoverageTypes", "CreatedAt", "DeletedAt", "EffectiveDate", "ExpirationDate", "OrganisationId", "PolicyNumber", "Status", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("0276497d-199d-4d7a-b166-de5643601ae6"), "Coastal Builders Group", "[\"Property\",\"Equipment\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2025, 3, 1), new DateOnly(2027, 2, 28), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2025-002001", "Active", null, null, null },
                    { new Guid("07516340-0391-4182-8b0d-4f507fad688b"), "Northwind Logistics Ltd", "[\"Vehicle\",\"Cargo\",\"Liability\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2025, 1, 1), new DateOnly(2030, 12, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2025-003001", "Active", null, null, null },
                    { new Guid("07a6b67a-e72d-4535-bd39-d299b55e434f"), "Keystone Manufacturing Co", "[\"Property\",\"Equipment\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2025, 1, 1), new DateOnly(2030, 12, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2025-003003", "Active", null, null, null },
                    { new Guid("5fd883ac-3109-4539-af13-c387cef26942"), "Archived Corp", "[\"Property\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2023-000099", "Expired", null, null, null },
                    { new Guid("8281fb25-5cd6-4f26-b82e-72695950beb3"), "Meridian Transport LLC", "[\"Vehicle\",\"Cargo\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2024, 1, 1), new DateOnly(2026, 12, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2024-001001", "Active", null, null, null },
                    { new Guid("8ab0d13d-392d-47d9-91b6-d6c2088f2de0"), "Stanton Medical Group", "[\"Liability\",\"Vehicle\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2025-002002", "Active", null, null, null },
                    { new Guid("95713a24-9905-4b0e-9d48-7518bb34be10"), "Summit Retail Holdings", "[\"Property\",\"Liability\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2025, 1, 1), new DateOnly(2030, 12, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2025-003002", "Active", null, null, null },
                    { new Guid("ae6abe22-49fa-4c63-950a-0e1022f61882"), "Harborview Properties Inc", "[\"Property\",\"Liability\"]", new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateOnly(2024, 6, 1), new DateOnly(2026, 5, 31), new Guid("b49c0515-77a9-49ff-be76-2a1fcfdc144f"), "POL-2024-001002", "Active", null, null, null }
                });

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
                name: "IX_ClaimAuditLog_ClaimId_CreatedAt",
                table: "ClaimAuditLog",
                columns: new[] { "ClaimId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAuditLog_ClaimId_EventType_CreatedAt",
                table: "ClaimAuditLog",
                columns: new[] { "ClaimId", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDocuments_ClaimId",
                table: "ClaimDocuments",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimParties_ClaimId_PartyRole_IsActive",
                table: "ClaimParties",
                columns: new[] { "ClaimId", "PartyRole", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_ClaimReserveComponents_ClaimId_Component",
                table: "ClaimReserveComponents",
                columns: new[] { "ClaimId", "Component" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRiskObjects_ClaimId",
                table: "ClaimRiskObjects",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_AssignedHandlerId",
                table: "Claims",
                column: "AssignedHandlerId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_OrganisationId_Status_UpdatedAt",
                table: "Claims",
                columns: new[] { "OrganisationId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PolicyId",
                table: "Claims",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "UX_Claims_OrganisationId_ClaimNumber",
                table: "Claims",
                columns: new[] { "OrganisationId", "ClaimNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClaimStatusTransitions_FromStatus_ToStatus",
                table: "ClaimStatusTransitions",
                columns: new[] { "FromStatus", "ToStatus" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClaimValidationIssues_ClaimId_RuleCode_Active",
                table: "ClaimValidationIssues",
                columns: new[] { "ClaimId", "RuleCode" },
                unique: true,
                filter: "[Status] IN (N'Open', N'Acknowledged') AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LossEvents_ClaimId",
                table: "LossEvents",
                column: "ClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LossEvents_LossDate",
                table: "LossEvents",
                column: "LossDate");

            migrationBuilder.CreateIndex(
                name: "IX_LossEvents_OrganisationId_CauseOfLossCode",
                table: "LossEvents",
                columns: new[] { "OrganisationId", "CauseOfLossCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_ClientName",
                table: "Policies",
                column: "ClientName");

            migrationBuilder.CreateIndex(
                name: "UX_Policies_OrganisationId_PolicyNumber",
                table: "Policies",
                columns: new[] { "OrganisationId", "PolicyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_ApprovalStatus_PostingStatus_ApprovedAt",
                table: "ReserveHistory",
                columns: new[] { "ApprovalStatus", "PostingStatus", "ApprovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReserveHistory_ClaimId_CreatedAt",
                table: "ReserveHistory",
                columns: new[] { "ClaimId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ReserveHistory_IdempotencyKey",
                table: "ReserveHistory",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ReserveHistory_ReserveComponentId_ChangeSequence",
                table: "ReserveHistory",
                columns: new[] { "ReserveComponentId", "ChangeSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ReserveHistory_ReserveComponentId_Pending",
                table: "ReserveHistory",
                column: "ReserveComponentId",
                unique: true,
                filter: "[ApprovalStatus] = N'PendingApproval' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganisationId",
                table: "Users",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            // BR-A-01 / D-14, defence in depth below the interceptor: the database itself refuses to
            // change or remove audit rows, whoever connects (a DENY does not stop dbo; a trigger does).
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_ClaimAuditLog_AppendOnly] ON [ClaimAuditLog]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'ClaimAuditLog is append-only (BR-A-01): UPDATE and DELETE are not permitted.', 1;
END");

            // D-14: the role the application user joins once it no longer connects as dbo (Phase 7).
            migrationBuilder.Sql(@"
IF DATABASE_PRINCIPAL_ID(N'claims_app') IS NULL
    CREATE ROLE [claims_app];");
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [ClaimAuditLog] TO [claims_app];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ClaimAuditLog_AppendOnly];");
            migrationBuilder.Sql("IF DATABASE_PRINCIPAL_ID(N'claims_app') IS NOT NULL DROP ROLE [claims_app];");

            migrationBuilder.DropTable(
                name: "ClaimAuditLog");

            migrationBuilder.DropTable(
                name: "ClaimDocuments");

            migrationBuilder.DropTable(
                name: "ClaimNumberCounters");

            migrationBuilder.DropTable(
                name: "ClaimParties");

            migrationBuilder.DropTable(
                name: "ClaimRiskObjects");

            migrationBuilder.DropTable(
                name: "ClaimStatusTransitions");

            migrationBuilder.DropTable(
                name: "ClaimValidationIssues");

            migrationBuilder.DropTable(
                name: "LossEvents");

            migrationBuilder.DropTable(
                name: "ReserveHistory");

            migrationBuilder.DropTable(
                name: "CauseOfLossCodes");

            migrationBuilder.DropTable(
                name: "ClaimReserveComponents");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Organisations");
        }
    }
}
