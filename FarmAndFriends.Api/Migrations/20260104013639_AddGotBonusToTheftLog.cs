using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGotBonusToTheftLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GotBonus",
                table: "TheftLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GotBonus",
                table: "TheftLogs");
        }
    }
}
