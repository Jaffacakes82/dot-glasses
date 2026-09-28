using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotGlasses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameLensPowerFieldsRangeNeutral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CustomSphereRight",
                table: "Tests",
                newName: "SphereRight");

            migrationBuilder.RenameColumn(
                name: "CustomSphereLeft",
                table: "Tests",
                newName: "SphereLeft");

            migrationBuilder.RenameColumn(
                name: "CustomCylinderRight",
                table: "Tests",
                newName: "CylinderRight");

            migrationBuilder.RenameColumn(
                name: "CustomCylinderLeft",
                table: "Tests",
                newName: "CylinderLeft");

            migrationBuilder.RenameColumn(
                name: "CustomAxisRight",
                table: "Tests",
                newName: "AxisRight");

            migrationBuilder.RenameColumn(
                name: "CustomAxisLeft",
                table: "Tests",
                newName: "AxisLeft");

            migrationBuilder.RenameColumn(
                name: "CustomAddPowerRight",
                table: "Tests",
                newName: "AddRight");

            migrationBuilder.RenameColumn(
                name: "CustomAddPowerLeft",
                table: "Tests",
                newName: "AddLeft");

            migrationBuilder.RenameColumn(
                name: "CustomSphereRight",
                table: "Sales",
                newName: "SphereRight");

            migrationBuilder.RenameColumn(
                name: "CustomSphereLeft",
                table: "Sales",
                newName: "SphereLeft");

            migrationBuilder.RenameColumn(
                name: "CustomCylinderRight",
                table: "Sales",
                newName: "CylinderRight");

            migrationBuilder.RenameColumn(
                name: "CustomCylinderLeft",
                table: "Sales",
                newName: "CylinderLeft");

            migrationBuilder.RenameColumn(
                name: "CustomAxisRight",
                table: "Sales",
                newName: "AxisRight");

            migrationBuilder.RenameColumn(
                name: "CustomAxisLeft",
                table: "Sales",
                newName: "AxisLeft");

            migrationBuilder.RenameColumn(
                name: "CustomAddPowerRight",
                table: "Sales",
                newName: "AddRight");

            migrationBuilder.RenameColumn(
                name: "CustomAddPowerLeft",
                table: "Sales",
                newName: "AddLeft");

            migrationBuilder.RenameColumn(
                name: "CustomSphereRight",
                table: "Leads",
                newName: "SphereRight");

            migrationBuilder.RenameColumn(
                name: "CustomSphereLeft",
                table: "Leads",
                newName: "SphereLeft");

            migrationBuilder.RenameColumn(
                name: "CustomCylinderRight",
                table: "Leads",
                newName: "CylinderRight");

            migrationBuilder.RenameColumn(
                name: "CustomCylinderLeft",
                table: "Leads",
                newName: "CylinderLeft");

            migrationBuilder.RenameColumn(
                name: "CustomAxisRight",
                table: "Leads",
                newName: "AxisRight");

            migrationBuilder.RenameColumn(
                name: "CustomAxisLeft",
                table: "Leads",
                newName: "AxisLeft");

            migrationBuilder.RenameColumn(
                name: "CustomAddPowerRight",
                table: "Leads",
                newName: "AddRight");

            migrationBuilder.RenameColumn(
                name: "CustomAddPowerLeft",
                table: "Leads",
                newName: "AddLeft");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SphereRight",
                table: "Tests",
                newName: "CustomSphereRight");

            migrationBuilder.RenameColumn(
                name: "SphereLeft",
                table: "Tests",
                newName: "CustomSphereLeft");

            migrationBuilder.RenameColumn(
                name: "CylinderRight",
                table: "Tests",
                newName: "CustomCylinderRight");

            migrationBuilder.RenameColumn(
                name: "CylinderLeft",
                table: "Tests",
                newName: "CustomCylinderLeft");

            migrationBuilder.RenameColumn(
                name: "AxisRight",
                table: "Tests",
                newName: "CustomAxisRight");

            migrationBuilder.RenameColumn(
                name: "AxisLeft",
                table: "Tests",
                newName: "CustomAxisLeft");

            migrationBuilder.RenameColumn(
                name: "AddRight",
                table: "Tests",
                newName: "CustomAddPowerRight");

            migrationBuilder.RenameColumn(
                name: "AddLeft",
                table: "Tests",
                newName: "CustomAddPowerLeft");

            migrationBuilder.RenameColumn(
                name: "SphereRight",
                table: "Sales",
                newName: "CustomSphereRight");

            migrationBuilder.RenameColumn(
                name: "SphereLeft",
                table: "Sales",
                newName: "CustomSphereLeft");

            migrationBuilder.RenameColumn(
                name: "CylinderRight",
                table: "Sales",
                newName: "CustomCylinderRight");

            migrationBuilder.RenameColumn(
                name: "CylinderLeft",
                table: "Sales",
                newName: "CustomCylinderLeft");

            migrationBuilder.RenameColumn(
                name: "AxisRight",
                table: "Sales",
                newName: "CustomAxisRight");

            migrationBuilder.RenameColumn(
                name: "AxisLeft",
                table: "Sales",
                newName: "CustomAxisLeft");

            migrationBuilder.RenameColumn(
                name: "AddRight",
                table: "Sales",
                newName: "CustomAddPowerRight");

            migrationBuilder.RenameColumn(
                name: "AddLeft",
                table: "Sales",
                newName: "CustomAddPowerLeft");

            migrationBuilder.RenameColumn(
                name: "SphereRight",
                table: "Leads",
                newName: "CustomSphereRight");

            migrationBuilder.RenameColumn(
                name: "SphereLeft",
                table: "Leads",
                newName: "CustomSphereLeft");

            migrationBuilder.RenameColumn(
                name: "CylinderRight",
                table: "Leads",
                newName: "CustomCylinderRight");

            migrationBuilder.RenameColumn(
                name: "CylinderLeft",
                table: "Leads",
                newName: "CustomCylinderLeft");

            migrationBuilder.RenameColumn(
                name: "AxisRight",
                table: "Leads",
                newName: "CustomAxisRight");

            migrationBuilder.RenameColumn(
                name: "AxisLeft",
                table: "Leads",
                newName: "CustomAxisLeft");

            migrationBuilder.RenameColumn(
                name: "AddRight",
                table: "Leads",
                newName: "CustomAddPowerRight");

            migrationBuilder.RenameColumn(
                name: "AddLeft",
                table: "Leads",
                newName: "CustomAddPowerLeft");
        }
    }
}
