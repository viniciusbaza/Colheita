using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultiHarvestCrops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CropName",
                table: "Seeds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HarvestCycles",
                table: "Seeds",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "RegrowTime",
                table: "Seeds",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentHarvestCycle",
                table: "Plots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentHarvestCycleStartedAt",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Seeds\" SET \"CropName\" = \"Name\" WHERE \"CropName\" IS NULL;");

            migrationBuilder.Sql(
                "UPDATE \"Plots\" SET \"CurrentHarvestCycle\" = 1, \"CurrentHarvestCycleStartedAt\" = \"PlantedAt\" WHERE \"SeedId\" IS NOT NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "CropName",
                table: "Seeds",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "carrot",
                columns: new[] { "CropName", "HarvestCycles", "RegrowTime" },
                values: new object[] { "Cenoura", 1, null });

            migrationBuilder.UpdateData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "corn",
                columns: new[] { "CropName", "HarvestCycles", "RegrowTime" },
                values: new object[] { "Milho", 1, null });

            migrationBuilder.UpdateData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "pumpkin",
                columns: new[] { "CropName", "HarvestCycles", "RegrowTime" },
                values: new object[] { "Abóbora", 1, null });

            migrationBuilder.UpdateData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "tomato",
                columns: new[] { "CropName", "HarvestCycles", "RegrowTime" },
                values: new object[] { "Tomate", 1, null });

            migrationBuilder.InsertData(
                table: "Seeds",
                columns: new[] { "Id", "BuyPrice", "CropAmount", "CropId", "CropName", "GrowTime", "HarvestCycles", "Icon", "MinLevel", "Name", "RegrowTime", "SellPrice", "TheftChancePercent" },
                values: new object[] { "apple_tree", 90, 3, "apple_crop", "Maçã", 7200.0, 3, "🍎", 5, "Macieira", 3600.0, 30, 100 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seeds_CropAmount_Positive",
                table: "Seeds",
                sql: "\"CropAmount\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seeds_GrowTime_Positive",
                table: "Seeds",
                sql: "\"GrowTime\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seeds_HarvestCycles_Positive",
                table: "Seeds",
                sql: "\"HarvestCycles\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seeds_RegrowTime_Consistent",
                table: "Seeds",
                sql: "(\"HarvestCycles\" = 1 AND \"RegrowTime\" IS NULL) OR (\"HarvestCycles\" > 1 AND \"RegrowTime\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Seeds_RequiredText_NotBlank",
                table: "Seeds",
                sql: "length(btrim(\"Id\")) > 0 AND length(btrim(\"Name\")) > 0 AND length(btrim(\"CropId\")) > 0 AND length(btrim(\"CropName\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycle_Consistent",
                table: "Plots",
                sql: "(\"SeedId\" IS NULL AND \"CurrentHarvestCycle\" IS NULL) OR (\"SeedId\" IS NOT NULL AND \"CurrentHarvestCycle\" IS NOT NULL AND \"CurrentHarvestCycle\" >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycleStartedAt_Consistent",
                table: "Plots",
                sql: "(\"SeedId\" IS NULL AND \"PlantedAt\" IS NULL AND \"ReadyAt\" IS NULL AND \"RemainingYield\" IS NULL AND \"CurrentHarvestCycleStartedAt\" IS NULL) OR (\"SeedId\" IS NOT NULL AND \"PlantedAt\" IS NOT NULL AND \"ReadyAt\" IS NOT NULL AND \"RemainingYield\" IS NOT NULL AND \"CurrentHarvestCycleStartedAt\" IS NOT NULL AND \"ReadyAt\" > \"CurrentHarvestCycleStartedAt\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_Quantity_NonNegative",
                table: "InventoryItems",
                sql: "\"Quantity\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_Quantity_NonNegative",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seeds_CropAmount_Positive",
                table: "Seeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seeds_GrowTime_Positive",
                table: "Seeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seeds_HarvestCycles_Positive",
                table: "Seeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seeds_RegrowTime_Consistent",
                table: "Seeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Seeds_RequiredText_NotBlank",
                table: "Seeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycle_Consistent",
                table: "Plots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycleStartedAt_Consistent",
                table: "Plots");

            migrationBuilder.DeleteData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "apple_tree");

            migrationBuilder.DropColumn(
                name: "CropName",
                table: "Seeds");

            migrationBuilder.DropColumn(
                name: "HarvestCycles",
                table: "Seeds");

            migrationBuilder.DropColumn(
                name: "RegrowTime",
                table: "Seeds");

            migrationBuilder.DropColumn(
                name: "CurrentHarvestCycle",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "CurrentHarvestCycleStartedAt",
                table: "Plots");
        }
    }
}
