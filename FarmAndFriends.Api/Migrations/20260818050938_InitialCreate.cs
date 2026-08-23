using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPestInfestationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farms", x => x.Id);
                    table.UniqueConstraint("AK_Farms_Id_UserId", x => new { x.Id, x.UserId });
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
                    table.CheckConstraint("CK_Inventories_Coins_NonNegative", "\"Coins\" >= 0");
                    table.CheckConstraint("CK_Inventories_PremiumCoins_NonNegative", "\"PremiumCoins\" >= 0");
                    table.ForeignKey(
                        name: "FK_Inventories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "PremiumCurrencyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerSequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    BalanceBefore = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EventReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OperationFingerprint = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    ExternalSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReversesTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumCurrencyTransactions", x => x.Id);
                    table.CheckConstraint("CK_PremiumCurrencyTransactions_Amount_NonZero", "\"Amount\" <> 0");
                    table.CheckConstraint("CK_PremiumCurrencyTransactions_Balance_Consistent", "\"BalanceAfter\" = \"BalanceBefore\" + \"Amount\"");
                    table.CheckConstraint("CK_PremiumCurrencyTransactions_BalanceAfter_NonNegative", "\"BalanceAfter\" >= 0");
                    table.CheckConstraint("CK_PremiumCurrencyTransactions_BalanceBefore_NonNegative", "\"BalanceBefore\" >= 0");
                    table.CheckConstraint("CK_PremiumCurrencyTransactions_ExternalReference_Complete", "(\"ExternalSource\" IS NULL AND \"ExternalTransactionId\" IS NULL) OR (\"ExternalSource\" IS NOT NULL AND \"ExternalTransactionId\" IS NOT NULL)");
                    table.CheckConstraint("CK_PremiumCurrencyTransactions_IdempotencyReference_Present", "\"IdempotencyKey\" IS NOT NULL OR \"ExternalSource\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_PremiumCurrencyTransactions_PremiumCurrencyTransactions_Rev~",
                        column: x => x.ReversesTransactionId,
                        principalTable: "PremiumCurrencyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PremiumCurrencyTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    CareOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    PestOccurrenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PestType = table.Column<int>(type: "integer", nullable: true),
                    PestStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PestScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PestAppearsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PestAppearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PestConsumesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PestResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PestConsumedAmount = table.Column<int>(type: "integer", nullable: false),
                    ProtectedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plots", x => x.Id);
                    table.UniqueConstraint("AK_Plots_Id_FarmId", x => new { x.Id, x.FarmId });
                    table.CheckConstraint("CK_Plots_Coordinates_NonNegative", "\"X\" >= 0 AND \"Y\" >= 0");
                    table.CheckConstraint("CK_Plots_PestConsumedAmount_NonNegative", "\"PestConsumedAmount\" >= 0");
                    table.CheckConstraint("CK_Plots_RemainingYield_Positive", "\"RemainingYield\" IS NULL OR \"RemainingYield\" >= 1");
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
                name: "PremiumCurrencyPurchaseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PremiumCurrencyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemIdSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPremiumPrice = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumCurrencyPurchaseItems", x => x.Id);
                    table.CheckConstraint("CK_PremiumCurrencyPurchaseItems_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_PremiumCurrencyPurchaseItems_UnitPrice_Positive", "\"UnitPremiumPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_PremiumCurrencyPurchaseItems_PremiumCurrencyTransactions_Pr~",
                        column: x => x.PremiumCurrencyTransactionId,
                        principalTable: "PremiumCurrencyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandPurchaseCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    PremiumCurrencyTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlotNumber = table.Column<int>(type: "integer", nullable: false),
                    PaymentCurrency = table.Column<string>(type: "text", nullable: false),
                    AmountSpent = table.Column<int>(type: "integer", nullable: false),
                    CoinsAfter = table.Column<int>(type: "integer", nullable: false),
                    PremiumCoinsAfter = table.Column<int>(type: "integer", nullable: false),
                    AddedPlotCount = table.Column<int>(type: "integer", nullable: false),
                    ExpandedWidth = table.Column<int>(type: "integer", nullable: true),
                    ExpandedHeight = table.Column<int>(type: "integer", nullable: true),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandPurchaseCompletions", x => x.Id);
                    table.CheckConstraint("CK_LandPurchaseCompletions_AmountSpent_Positive", "\"AmountSpent\" > 0");
                    table.CheckConstraint("CK_LandPurchaseCompletions_CoinsAfter_NonNegative", "\"CoinsAfter\" >= 0");
                    table.CheckConstraint("CK_LandPurchaseCompletions_ExpansionMetadata_Consistent", "(\"PlotNumber\" = 9 AND \"AddedPlotCount\" = 19 AND \"ExpandedWidth\" = 7 AND \"ExpandedHeight\" = 4) OR (\"PlotNumber\" <> 9 AND \"AddedPlotCount\" = 0 AND \"ExpandedWidth\" IS NULL AND \"ExpandedHeight\" IS NULL)");
                    table.CheckConstraint("CK_LandPurchaseCompletions_PaymentCurrency_Valid", "\"PaymentCurrency\" IN ('coins', 'premiumCoins')");
                    table.CheckConstraint("CK_LandPurchaseCompletions_PlotNumber_Range", "\"PlotNumber\" >= 7 AND \"PlotNumber\" <= 28");
                    table.CheckConstraint("CK_LandPurchaseCompletions_PremiumCoinsAfter_NonNegative", "\"PremiumCoinsAfter\" >= 0");
                    table.CheckConstraint("CK_LandPurchaseCompletions_PremiumLedger_Consistent", "(\"PaymentCurrency\" = 'premiumCoins' AND \"PremiumCurrencyTransactionId\" IS NOT NULL) OR (\"PaymentCurrency\" = 'coins' AND \"PremiumCurrencyTransactionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_LandPurchaseCompletions_Farms_FarmId_BuyerUserId",
                        columns: x => new { x.FarmId, x.BuyerUserId },
                        principalTable: "Farms",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandPurchaseCompletions_Plots_PlotId_FarmId",
                        columns: x => new { x.PlotId, x.FarmId },
                        principalTable: "Plots",
                        principalColumns: new[] { "Id", "FarmId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandPurchaseCompletions_PremiumCurrencyTransactions_Premium~",
                        column: x => x.PremiumCurrencyTransactionId,
                        principalTable: "PremiumCurrencyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandPurchaseCompletions_Users_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    PestPlotId = table.Column<Guid>(type: "uuid", nullable: true),
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
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId",
                table: "InventoryItems",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LandPurchaseCompletions_BuyerUserId_IdempotencyKey",
                table: "LandPurchaseCompletions",
                columns: new[] { "BuyerUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandPurchaseCompletions_FarmId_BuyerUserId",
                table: "LandPurchaseCompletions",
                columns: new[] { "FarmId", "BuyerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LandPurchaseCompletions_FarmId_PlotNumber",
                table: "LandPurchaseCompletions",
                columns: new[] { "FarmId", "PlotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandPurchaseCompletions_PlotId",
                table: "LandPurchaseCompletions",
                column: "PlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandPurchaseCompletions_PlotId_FarmId",
                table: "LandPurchaseCompletions",
                columns: new[] { "PlotId", "FarmId" });

            migrationBuilder.CreateIndex(
                name: "IX_LandPurchaseCompletions_PremiumCurrencyTransactionId",
                table: "LandPurchaseCompletions",
                column: "PremiumCurrencyTransactionId",
                unique: true);

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
                name: "IX_Notifications_PestPlotId_CreatedAt",
                table: "Notifications",
                columns: new[] { "PestPlotId", "CreatedAt" });

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

            migrationBuilder.CreateIndex(
                name: "IX_Plots_CareOpportunityId",
                table: "Plots",
                column: "CareOpportunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plots_FarmId_X_Y",
                table: "Plots",
                columns: new[] { "FarmId", "X", "Y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plots_PestOccurrenceId",
                table: "Plots",
                column: "PestOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyPurchaseItems_PremiumCurrencyTransactionId",
                table: "PremiumCurrencyPurchaseItems",
                column: "PremiumCurrencyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyTransactions_EventType_EventReference",
                table: "PremiumCurrencyTransactions",
                columns: new[] { "EventType", "EventReference" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyTransactions_ExternalSource_ExternalTransact~",
                table: "PremiumCurrencyTransactions",
                columns: new[] { "ExternalSource", "ExternalTransactionId" },
                unique: true,
                filter: "\"ExternalSource\" IS NOT NULL AND \"ExternalTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyTransactions_LedgerSequence",
                table: "PremiumCurrencyTransactions",
                column: "LedgerSequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyTransactions_ReversesTransactionId",
                table: "PremiumCurrencyTransactions",
                column: "ReversesTransactionId",
                unique: true,
                filter: "\"ReversesTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyTransactions_UserId_IdempotencyKey",
                table: "PremiumCurrencyTransactions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumCurrencyTransactions_UserId_LedgerSequence",
                table: "PremiumCurrencyTransactions",
                columns: new[] { "UserId", "LedgerSequence" },
                descending: new[] { false, true });

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
                name: "LandPurchaseCompletions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PestRemovalCompletions");

            migrationBuilder.DropTable(
                name: "PremiumCurrencyPurchaseItems");

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
                name: "PremiumCurrencyTransactions");

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
