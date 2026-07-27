using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Seeds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    BuyPrice = table.Column<int>(type: "integer", nullable: false),
                    SellPrice = table.Column<int>(type: "integer", nullable: false),
                    GrowTime = table.Column<double>(type: "double precision", nullable: false),
                    TheftChancePercent = table.Column<int>(type: "integer", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    CropId = table.Column<string>(type: "text", nullable: false),
                    CropAmount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    NormalizedUsername = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CurrentXp = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Farms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Farms_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserBId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                    table.CheckConstraint("CK_Friendships_DifferentUsers", "\"UserAId\" <> \"UserBId\"");
                    table.CheckConstraint("CK_Friendships_RequesterIsParticipant", "\"RequestedByUserId\" = \"UserAId\" OR \"RequestedByUserId\" = \"UserBId\"");
                    table.ForeignKey(
                        name: "FK_Friendships_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Friendships_Users_UserAId",
                        column: x => x.UserAId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Friendships_Users_UserBId",
                        column: x => x.UserBId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Coins = table.Column<int>(type: "integer", nullable: false),
                    PremiumCoins = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Plots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Unlocked = table.Column<bool>(type: "boolean", nullable: false),
                    SeedId = table.Column<string>(type: "text", nullable: true),
                    PlantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemainingYield = table.Column<int>(type: "integer", nullable: true),
                    CareOpportunityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plots_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitorFarmCareCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RewardGranted = table.Column<bool>(type: "boolean", nullable: false),
                    CoinsReward = table.Column<int>(type: "integer", nullable: false),
                    XpReward = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorFarmCareCycles", x => x.Id);
                    table.CheckConstraint("CK_VisitorFarmCareCycles_DifferentUsers", "\"VisitorUserId\" <> \"OwnerUserId\"");
                    table.CheckConstraint("CK_VisitorFarmCareCycles_ValidWindow", "\"EndsAt\" > \"StartedAt\"");
                    table.ForeignKey(
                        name: "FK_VisitorFarmCareCycles_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitorFarmCareCycles_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitorFarmCareCycles_Users_VisitorUserId",
                        column: x => x.VisitorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TheftLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThiefUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeedId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    GotBonus = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheftLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheftLogs_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TheftLogs_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TheftLogs_Seeds_SeedId",
                        column: x => x.SeedId,
                        principalTable: "Seeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TheftLogs_Users_ThiefUserId",
                        column: x => x.ThiefUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CropCareCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitorFarmCareCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CaredByUsername = table.Column<string>(type: "text", nullable: false),
                    CoinsGained = table.Column<int>(type: "integer", nullable: false),
                    XpGained = table.Column<int>(type: "integer", nullable: false),
                    CoinsAfter = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropCareCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropCareCompletions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropCareCompletions_Users_VisitorUserId",
                        column: x => x.VisitorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropCareCompletions_VisitorFarmCareCycles_VisitorFarmCareCy~",
                        column: x => x.VisitorFarmCareCycleId,
                        principalTable: "VisitorFarmCareCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    FriendshipId = table.Column<Guid>(type: "uuid", nullable: true),
                    TheftLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    CareOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Friendships_FriendshipId",
                        column: x => x.FriendshipId,
                        principalTable: "Friendships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_TheftLogs_TheftLogId",
                        column: x => x.TheftLogId,
                        principalTable: "TheftLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Seeds",
                columns: new[] { "Id", "BuyPrice", "CropAmount", "CropId", "GrowTime", "Icon", "MinLevel", "Name", "SellPrice", "TheftChancePercent" },
                values: new object[,]
                {
                    { "carrot", 10, 1, "carrot_crop", 300.0, "🥕", 1, "Cenoura", 20, 100 },
                    { "corn", 20, 3, "corn_crop", 120.0, "🌽", 2, "Milho", 45, 100 },
                    { "pumpkin", 40, 5, "pumpkin_crop", 120.0, "🎃", 4, "Abóbora", 80, 100 },
                    { "tomato", 30, 4, "tomato_crop", 120.0, "🍅", 3, "Tomate", 60, 100 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_CareOpportunityId_CaredAt",
                table: "CropCareCompletions",
                columns: new[] { "CareOpportunityId", "CaredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_OwnerUserId",
                table: "CropCareCompletions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_VisitorFarmCareCycleId_CareOpportunityId",
                table: "CropCareCompletions",
                columns: new[] { "VisitorFarmCareCycleId", "CareOpportunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_VisitorUserId_CaredAt",
                table: "CropCareCompletions",
                columns: new[] { "VisitorUserId", "CaredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_VisitorUserId_CareOpportunityId_CaredAt",
                table: "CropCareCompletions",
                columns: new[] { "VisitorUserId", "CareOpportunityId", "CaredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_VisitorUserId_IdempotencyKey",
                table: "CropCareCompletions",
                columns: new[] { "VisitorUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CropCareCompletions_VisitorUserId_OwnerUserId_CaredAt",
                table: "CropCareCompletions",
                columns: new[] { "VisitorUserId", "OwnerUserId", "CaredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Farms_UserId",
                table: "Farms",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequestedByUserId",
                table: "Friendships",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Status_RequestedByUserId",
                table: "Friendships",
                columns: new[] { "Status", "RequestedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserAId_UserBId",
                table: "Friendships",
                columns: new[] { "UserAId", "UserBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserBId",
                table: "Friendships",
                column: "UserBId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_UserId",
                table: "Inventories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId",
                table: "InventoryItems",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ActorUserId",
                table: "Notifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CareOpportunityId",
                table: "Notifications",
                column: "CareOpportunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_FriendshipId",
                table: "Notifications",
                column: "FriendshipId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_ReadAt_CreatedAt",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TheftLogId",
                table: "Notifications",
                column: "TheftLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Type_ActorUserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "Type", "ActorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Type_ActorUserId_RecipientUserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "Type", "ActorUserId", "RecipientUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Plots_CareOpportunityId",
                table: "Plots",
                column: "CareOpportunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plots_FarmId",
                table: "Plots",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TheftLogs_FarmId_ThiefUserId_CreatedAt",
                table: "TheftLogs",
                columns: new[] { "FarmId", "ThiefUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TheftLogs_PlotId",
                table: "TheftLogs",
                column: "PlotId");

            migrationBuilder.CreateIndex(
                name: "IX_TheftLogs_SeedId",
                table: "TheftLogs",
                column: "SeedId");

            migrationBuilder.CreateIndex(
                name: "IX_TheftLogs_ThiefUserId",
                table: "TheftLogs",
                column: "ThiefUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareCycles_Visitor_Farm_Rewarded_StartedAt",
                table: "VisitorFarmCareCycles",
                columns: new[] { "VisitorUserId", "FarmId", "RewardGranted", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorFarmCareCycles_FarmId",
                table: "VisitorFarmCareCycles",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorFarmCareCycles_OwnerUserId",
                table: "VisitorFarmCareCycles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorFarmCareCycles_VisitorUserId_FarmId_StartedAt",
                table: "VisitorFarmCareCycles",
                columns: new[] { "VisitorUserId", "FarmId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropCareCompletions");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "VisitorFarmCareCycles");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "TheftLogs");

            migrationBuilder.DropTable(
                name: "Plots");

            migrationBuilder.DropTable(
                name: "Seeds");

            migrationBuilder.DropTable(
                name: "Farms");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
