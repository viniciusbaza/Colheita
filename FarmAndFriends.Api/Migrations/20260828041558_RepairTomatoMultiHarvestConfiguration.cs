using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class RepairTomatoMultiHarvestConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "tomato",
                columns: new[] { "HarvestCycles", "RegrowTime" },
                values: new object[] { 2, 120.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "tomato",
                columns: new[] { "HarvestCycles", "RegrowTime" },
                values: new object[] { 1, null });
        }
    }
}
