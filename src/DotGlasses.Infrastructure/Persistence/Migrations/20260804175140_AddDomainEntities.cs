using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HierarchyPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HierarchyPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TechnicianUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTestId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    OccupationRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccupationOtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConsentGiven = table.Column<bool>(type: "boolean", nullable: false),
                    ReasonNotPurchasedRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonNotPurchasedOtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LensRangeType = table.Column<int>(type: "integer", nullable: true),
                    PresetCatalogueId = table.Column<Guid>(type: "uuid", nullable: true),
                    LensOptionLeftId = table.Column<Guid>(type: "uuid", nullable: true),
                    LensOptionRightId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomSphereLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomCylinderLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAxisLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAddPowerLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomSphereRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomCylinderRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAxisRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAddPowerRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    PupilDistanceMm = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    ChildrensFrame = table.Column<bool>(type: "boolean", nullable: false),
                    CoatingPreferenceRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedFlag = table.Column<bool>(type: "boolean", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LensOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PresetCatalogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SphericalPower = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    IsBifocal = table.Column<bool>(type: "boolean", nullable: false),
                    AddPower = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    CoatingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LensOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HierarchyPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsTrainingOrg = table.Column<bool>(type: "boolean", nullable: false),
                    CanHandleCustomOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationNodes_OrganisationNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrganisationNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresetCatalogueAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PresetCatalogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetCatalogueAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PresetCatalogues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwningOrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetCatalogues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceDataItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsOtherOption = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDataItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HierarchyPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TechnicianUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    OccupationRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccupationOtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConsentGiven = table.Column<bool>(type: "boolean", nullable: false),
                    LensRangeType = table.Column<int>(type: "integer", nullable: false),
                    PresetCatalogueId = table.Column<Guid>(type: "uuid", nullable: true),
                    LensOptionLeftId = table.Column<Guid>(type: "uuid", nullable: true),
                    LensOptionRightId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomSphereLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomCylinderLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAxisLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAddPowerLeft = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomSphereRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomCylinderRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAxisRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CustomAddPowerRight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    OrderFromDotGlasses = table.Column<bool>(type: "boolean", nullable: false),
                    PupilDistanceMm = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    ChildrensFrame = table.Column<bool>(type: "boolean", nullable: false),
                    FrameColourRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameColourOtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FrameCoverage = table.Column<int>(type: "integer", nullable: false),
                    CoatingRefId = table.Column<Guid>(type: "uuid", nullable: false),
                    HardCaseSold = table.Column<bool>(type: "boolean", nullable: false),
                    HardCaseColourRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    HardCaseOtherColourText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HierarchyPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TechnicianUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    OccupationRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccupationOtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ReferralReasonRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferralOtherText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferralLocationFreeText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConvertedToLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserOrgAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOrgAssignments", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LensOptions",
                columns: new[] { "Id", "AddPower", "CoatingId", "IsBifocal", "PresetCatalogueId", "SortOrder", "SphericalPower" },
                values: new object[,]
                {
                    { new Guid("d0000000-0000-0000-0000-000000000001"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000001"), 0, 2.50m },
                    { new Guid("d0000000-0000-0000-0000-000000000002"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000001"), 1, 1.25m },
                    { new Guid("d0000000-0000-0000-0000-000000000003"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000001"), 2, 0.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000004"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000001"), 3, -1.50m },
                    { new Guid("d0000000-0000-0000-0000-000000000005"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000001"), 4, -3.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000006"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000001"), 5, -4.50m },
                    { new Guid("d0000000-0000-0000-0000-000000000007"), 2.50m, new Guid("b0000000-0000-0000-0000-000000000023"), true, new Guid("c0000000-0000-0000-0000-000000000001"), 6, 0.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000008"), 1.25m, new Guid("b0000000-0000-0000-0000-000000000023"), true, new Guid("c0000000-0000-0000-0000-000000000001"), 7, 0.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000009"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 0, 3.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000010"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 1, 2.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000011"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 2, 1.25m },
                    { new Guid("d0000000-0000-0000-0000-000000000012"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 3, 0.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000013"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 4, -1.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000014"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 5, -1.50m },
                    { new Guid("d0000000-0000-0000-0000-000000000015"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 6, -2.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000016"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 7, -2.50m },
                    { new Guid("d0000000-0000-0000-0000-000000000017"), null, new Guid("b0000000-0000-0000-0000-000000000024"), false, new Guid("c0000000-0000-0000-0000-000000000002"), 8, -4.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000018"), 3.00m, new Guid("b0000000-0000-0000-0000-000000000023"), true, new Guid("c0000000-0000-0000-0000-000000000002"), 9, 0.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000019"), 2.00m, new Guid("b0000000-0000-0000-0000-000000000023"), true, new Guid("c0000000-0000-0000-0000-000000000002"), 10, 0.00m },
                    { new Guid("d0000000-0000-0000-0000-000000000020"), 1.25m, new Guid("b0000000-0000-0000-0000-000000000023"), true, new Guid("c0000000-0000-0000-0000-000000000002"), 11, 0.00m }
                });

            migrationBuilder.InsertData(
                table: "OrganisationNodes",
                columns: new[] { "Id", "CanHandleCustomOrders", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "HierarchyPath", "IsDeleted", "IsTrainingOrg", "Kind", "Level", "ModifiedAtUtc", "ModifiedBy", "Name", "ParentId" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000001"), false, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "/1/", false, false, null, 0, null, null, "DOT Glasses International", null });

            migrationBuilder.InsertData(
                table: "PresetCatalogueAssignments",
                columns: new[] { "Id", "CreatedAtUtc", "OrgNodeId", "PresetCatalogueId" },
                values: new object[,]
                {
                    { new Guid("e0000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("a0000000-0000-0000-0000-000000000002"), new Guid("c0000000-0000-0000-0000-000000000001") },
                    { new Guid("e0000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("a0000000-0000-0000-0000-000000000002"), new Guid("c0000000-0000-0000-0000-000000000002") }
                });

            migrationBuilder.InsertData(
                table: "PresetCatalogues",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "Name", "OwningOrgNodeId" },
                values: new object[,]
                {
                    { new Guid("c0000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, null, null, "6-Lens Set", new Guid("a0000000-0000-0000-0000-000000000001") },
                    { new Guid("c0000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, null, null, "9-Lens Set", new Guid("a0000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "ReferenceDataItems",
                columns: new[] { "Id", "Category", "Code", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "IsActive", "IsDeleted", "IsOtherOption", "Label", "ModifiedAtUtc", "ModifiedBy", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("b0000000-0000-0000-0000-000000000001"), 0, "farmer", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Farmer", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000002"), 0, "factory_worker", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Factory worker", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000003"), 0, "teacher", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Teacher", null, null, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000004"), 0, "health", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Health worker", null, null, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000005"), 0, "salaried", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Other salaried employee", null, null, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000006"), 0, "business_owner", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Business owner", null, null, 5 },
                    { new Guid("b0000000-0000-0000-0000-000000000007"), 0, "labourer", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Casual labour", null, null, 6 },
                    { new Guid("b0000000-0000-0000-0000-000000000008"), 0, "fundi", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Fundi", null, null, 7 },
                    { new Guid("b0000000-0000-0000-0000-000000000009"), 0, "retired", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Retired", null, null, 8 },
                    { new Guid("b0000000-0000-0000-0000-000000000010"), 0, "student", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Student", null, null, 9 },
                    { new Guid("b0000000-0000-0000-0000-000000000011"), 0, "none", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "No economic activity", null, null, 10 },
                    { new Guid("b0000000-0000-0000-0000-000000000012"), 0, "other", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, true, "Other", null, null, 11 },
                    { new Guid("b0000000-0000-0000-0000-000000000013"), 1, "glasses_didnt_help", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "These glasses couldn't help me", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000014"), 1, "dont_need_glasses", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Don't need glasses", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000015"), 1, "price", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Price", null, null, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000016"), 1, "no_money", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Don't have the money now", null, null, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000017"), 1, "consulting_family", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Needed to consult family", null, null, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000018"), 1, "returning_later", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Wanted to return later", null, null, 5 },
                    { new Guid("b0000000-0000-0000-0000-000000000019"), 1, "want_other_provider", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Preferred another provider", null, null, 6 },
                    { new Guid("b0000000-0000-0000-0000-000000000020"), 1, "not_convinced_of_benefit", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Not convinced of benefit", null, null, 7 },
                    { new Guid("b0000000-0000-0000-0000-000000000021"), 1, "other", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, true, "Other", null, null, 8 },
                    { new Guid("b0000000-0000-0000-0000-000000000022"), 2, "inconclusive", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Inconclusive test result", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000023"), 3, "photochromic", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Photochromic", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000024"), 3, "clear", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Clear", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000025"), 3, "blue_block", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Blue block", null, null, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000026"), 3, "polarized", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Polarized", null, null, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000027"), 3, "sunglasses", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Sunglasses", null, null, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000028"), 4, "black", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Black", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000029"), 4, "blue", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Blue", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000030"), 2, "suspected_eye_disease", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Suspected eye disease", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000031"), 2, "high_prescription", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "High power requirement or outside Dot Glasses range", null, null, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000032"), 2, "astigmatism", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Astigmatism", null, null, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000033"), 2, "young_child", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Child under eligible age without approval from a specialist", null, null, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000034"), 2, "other", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, true, "Other", null, null, 5 },
                    { new Guid("b0000000-0000-0000-0000-000000000035"), 4, "blue_black", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Blue-Black", null, null, 2 },
                    { new Guid("b0000000-0000-0000-0000-000000000036"), 4, "brown_black", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Brown-Black", null, null, 3 },
                    { new Guid("b0000000-0000-0000-0000-000000000037"), 4, "purple", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Purple", null, null, 4 },
                    { new Guid("b0000000-0000-0000-0000-000000000038"), 4, "purple_black", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Purple-Black", null, null, 5 },
                    { new Guid("b0000000-0000-0000-0000-000000000039"), 4, "other", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, true, "Other", null, null, 6 },
                    { new Guid("b0000000-0000-0000-0000-000000000040"), 5, "orange", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Orange", null, null, 0 },
                    { new Guid("b0000000-0000-0000-0000-000000000041"), 5, "green", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, false, "Green", null, null, 1 },
                    { new Guid("b0000000-0000-0000-0000-000000000042"), 5, "other", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, true, "Other", null, null, 2 }
                });

            migrationBuilder.InsertData(
                table: "OrganisationNodes",
                columns: new[] { "Id", "CanHandleCustomOrders", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "HierarchyPath", "IsDeleted", "IsTrainingOrg", "Kind", "Level", "ModifiedAtUtc", "ModifiedBy", "Name", "ParentId" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000002"), true, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "/1/2/", false, false, null, 1, null, null, "Kenya", new Guid("a0000000-0000-0000-0000-000000000001") },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), false, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "/1/2/3/", false, false, "Retailer", 2, null, null, "Kangemi Vision Centre", new Guid("a0000000-0000-0000-0000-000000000002") },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), false, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "/1/2/3/4/", false, false, "Standalone", 3, null, null, "Kangemi Vision Centre — Outreach Post", new Guid("a0000000-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_HierarchyPath",
                table: "Customers",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_HierarchyPath_FullName",
                table: "Customers",
                columns: new[] { "HierarchyPath", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_HierarchyPath_PhoneNumber",
                table: "Customers",
                columns: new[] { "HierarchyPath", "PhoneNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CustomerId",
                table: "Leads",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_HierarchyPath",
                table: "Leads",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TechnicianUserId",
                table: "Leads",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LensOptions_CoatingId",
                table: "LensOptions",
                column: "CoatingId");

            migrationBuilder.CreateIndex(
                name: "IX_LensOptions_PresetCatalogueId",
                table: "LensOptions",
                column: "PresetCatalogueId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationNodes_HierarchyPath",
                table: "OrganisationNodes",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationNodes_ParentId",
                table: "OrganisationNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PresetCatalogueAssignments_OrgNodeId",
                table: "PresetCatalogueAssignments",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PresetCatalogueAssignments_PresetCatalogueId",
                table: "PresetCatalogueAssignments",
                column: "PresetCatalogueId");

            migrationBuilder.CreateIndex(
                name: "IX_PresetCatalogueAssignments_PresetCatalogueId_OrgNodeId",
                table: "PresetCatalogueAssignments",
                columns: new[] { "PresetCatalogueId", "OrgNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PresetCatalogues_OwningOrgNodeId",
                table: "PresetCatalogues",
                column: "OwningOrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDataItems_Category_Code",
                table: "ReferenceDataItems",
                columns: new[] { "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_HierarchyPath",
                table: "Sales",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TechnicianUserId",
                table: "Sales",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tests_CustomerId",
                table: "Tests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tests_HierarchyPath",
                table: "Tests",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_Tests_TechnicianUserId",
                table: "Tests",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrgAssignments_OrgNodeId",
                table: "UserOrgAssignments",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrgAssignments_UserId",
                table: "UserOrgAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrgAssignments_UserId_OrgNodeId",
                table: "UserOrgAssignments",
                columns: new[] { "UserId", "OrgNodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "LensOptions");

            migrationBuilder.DropTable(
                name: "OrganisationNodes");

            migrationBuilder.DropTable(
                name: "PresetCatalogueAssignments");

            migrationBuilder.DropTable(
                name: "PresetCatalogues");

            migrationBuilder.DropTable(
                name: "ReferenceDataItems");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Tests");

            migrationBuilder.DropTable(
                name: "UserOrgAssignments");
        }
    }
}
