using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLensFieldsToTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChildrensFrame",
                table: "Tests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CoatingPreferenceRefId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomAddPowerLeft",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomAddPowerRight",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomAxisLeft",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomAxisRight",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomCylinderLeft",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomCylinderRight",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomSphereLeft",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomSphereRight",
                table: "Tests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionLeftId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensOptionRightId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LensRangeType",
                table: "Tests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LensTypeOtherText",
                table: "Tests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LensTypeRefId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PresetCatalogueId",
                table: "Tests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PresetPupilDistanceBucket",
                table: "Tests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PupilDistanceMm",
                table: "Tests",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChildrensFrame",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CoatingPreferenceRefId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomAddPowerLeft",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomAddPowerRight",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomAxisLeft",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomAxisRight",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomCylinderLeft",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomCylinderRight",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomSphereLeft",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CustomSphereRight",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensOptionLeftId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensOptionRightId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensRangeType",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensTypeOtherText",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LensTypeRefId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "PresetCatalogueId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "PresetPupilDistanceBucket",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "PupilDistanceMm",
                table: "Tests");
        }
    }
}
