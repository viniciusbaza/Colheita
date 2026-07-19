using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTheftNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TheftLogId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TheftLogId",
                table: "Notifications",
                column: "TheftLogId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_TheftLogs_TheftLogId",
                table: "Notifications",
                column: "TheftLogId",
                principalTable: "TheftLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_TheftLogs_TheftLogId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TheftLogId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TheftLogId",
                table: "Notifications");
        }
    }
}
