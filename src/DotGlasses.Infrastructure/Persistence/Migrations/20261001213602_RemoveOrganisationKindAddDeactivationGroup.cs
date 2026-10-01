using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrganisationKindAddDeactivationGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "OrganisationNodes");

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivationGroupId",
                table: "OrganisationNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "DeactivationGroupId",
                value: null);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "DeactivationGroupId",
                value: null);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "DeactivationGroupId",
                value: null);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "DeactivationGroupId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationNodes_DeactivationGroupId",
                table: "OrganisationNodes",
                column: "DeactivationGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganisationNodes_DeactivationGroupId",
                table: "OrganisationNodes");

            migrationBuilder.DropColumn(
                name: "DeactivationGroupId",
                table: "OrganisationNodes");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "OrganisationNodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "Kind",
                value: null);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "Kind",
                value: null);

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "Kind",
                value: "Retailer");

            migrationBuilder.UpdateData(
                table: "OrganisationNodes",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "Kind",
                value: "Standalone");
        }
    }
}
