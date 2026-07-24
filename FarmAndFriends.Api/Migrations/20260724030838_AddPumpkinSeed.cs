using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPumpkinSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Seeds",
                columns: new[] { "Id", "BuyPrice", "CropAmount", "CropId", "GrowTime", "Icon", "MinLevel", "Name", "SellPrice", "TheftChancePercent" },
                values: new object[] { "pumpkin", 40, 5, "pumpkin_crop", 120.0, "🎃", 4, "Abóbora", 80, 100 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Seeds",
                keyColumn: "Id",
                keyValue: "pumpkin");
        }
    }
}
