using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferredOrTreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReferredOrTreated",
                table: "Tests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TreatedInFacility",
                table: "Tests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralLocationFreeText",
                table: "Sales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralOtherText",
                table: "Sales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferralReasonRefId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReferredOrTreated",
                table: "Sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TreatedInFacility",
                table: "Sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralLocationFreeText",
                table: "Leads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralOtherText",
                table: "Leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferralReasonRefId",
                table: "Leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReferredOrTreated",
                table: "Leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TreatedInFacility",
                table: "Leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // TestOutcome.Referred (=2) was retired as an enum member (2026-09-03 — "referred or
            // treated" is now an orthogonal flag, not an outcome value). Any row still holding the
            // old raw value 2 would otherwise map to an undefined C# enum value and throw the
            // moment it's read back through ToContractOutcome — reclassify as NeedsGlasses (the
            // closest surviving outcome — a referral implies the person needed follow-up care) and
            // set ReferredOrTreated so the fact a referral happened isn't silently lost.
            migrationBuilder.Sql(
                """
                UPDATE "Tests" SET "Outcome" = 1, "ReferredOrTreated" = true WHERE "Outcome" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferredOrTreated",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TreatedInFacility",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "ReferralLocationFreeText",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReferralOtherText",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReferralReasonRefId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReferredOrTreated",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TreatedInFacility",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReferralLocationFreeText",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ReferralOtherText",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ReferralReasonRefId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ReferredOrTreated",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "TreatedInFacility",
                table: "Leads");
        }
    }
}
