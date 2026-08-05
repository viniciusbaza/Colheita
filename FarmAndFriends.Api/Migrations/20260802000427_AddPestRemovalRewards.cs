using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPestRemovalRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PestOccurrenceId",
                table: "Plots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PestRemovalCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PestOccurrenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemainingYieldAfter = table.Column<int>(type: "integer", nullable: true),
                    CoinsGained = table.Column<int>(type: "integer", nullable: false),
                    XpGained = table.Column<int>(type: "integer", nullable: false),
                    CoinsAfter = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestRemovalCompletions", x => x.Id);
                    table.CheckConstraint("CK_PestRemovalCompletions_CoinsAfter_NonNegative", "\"CoinsAfter\" >= 0");
                    table.CheckConstraint("CK_PestRemovalCompletions_CoinsGained_NonNegative", "\"CoinsGained\" >= 0");
                    table.CheckConstraint("CK_PestRemovalCompletions_RemainingYield_Positive", "\"RemainingYieldAfter\" IS NULL OR \"RemainingYieldAfter\" >= 1");
                    table.CheckConstraint("CK_PestRemovalCompletions_XpGained_NonNegative", "\"XpGained\" >= 0");
                    table.ForeignKey(
                        name: "FK_PestRemovalCompletions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PestRemovalCompletions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plots_PestOccurrenceId",
                table: "Plots",
                column: "PestOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PestRemovalCompletions_ActorUserId_IdempotencyKey",
                table: "PestRemovalCompletions",
                columns: new[] { "ActorUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PestRemovalCompletions_ActorUserId_RemovedAt",
                table: "PestRemovalCompletions",
                columns: new[] { "ActorUserId", "RemovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PestRemovalCompletions_OwnerUserId",
                table: "PestRemovalCompletions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PestRemovalCompletions_PestOccurrenceId",
                table: "PestRemovalCompletions",
                column: "PestOccurrenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PestRemovalCompletions");

            migrationBuilder.DropIndex(
                name: "IX_Plots_PestOccurrenceId",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "PestOccurrenceId",
                table: "Plots");
        }
    }
}
