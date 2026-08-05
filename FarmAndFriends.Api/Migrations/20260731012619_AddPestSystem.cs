using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPestSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PestAppearedAt",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PestAppearsAt",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PestConsumedAmount",
                table: "Plots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PestConsumesAt",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PestResolvedAt",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PestScheduledAt",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PestStatus",
                table: "Plots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PestType",
                table: "Plots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProtectedUntil",
                table: "Plots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PestPlotId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPestInfestationAt",
                table: "Farms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Plots"
                SET "RemainingYield" = NULL
                WHERE "SeedId" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Plots" AS p
                SET "RemainingYield" = GREATEST(
                    1,
                    s."CropAmount" - COALESCE((
                        SELECT SUM(t."Quantity")
                        FROM "TheftLogs" AS t
                        WHERE t."PlotId" = p."Id"
                          AND (p."PlantedAt" IS NULL
                               OR t."CreatedAt" >= p."PlantedAt")
                    ), 0))
                FROM "Seeds" AS s
                WHERE p."SeedId" = s."Id"
                  AND (p."RemainingYield" IS NULL
                       OR p."RemainingYield" < 1);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plots_PestConsumedAmount_NonNegative",
                table: "Plots",
                sql: "\"PestConsumedAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plots_RemainingYield_Positive",
                table: "Plots",
                sql: "\"RemainingYield\" IS NULL OR \"RemainingYield\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PestPlotId_CreatedAt",
                table: "Notifications",
                columns: new[] { "PestPlotId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Plots_PestConsumedAmount_NonNegative",
                table: "Plots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plots_RemainingYield_Positive",
                table: "Plots");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_PestPlotId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PestAppearedAt",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestAppearsAt",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestConsumedAmount",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestConsumesAt",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestResolvedAt",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestScheduledAt",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestStatus",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestType",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "ProtectedUntil",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestPlotId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastPestInfestationAt",
                table: "Farms");
        }
    }
}
