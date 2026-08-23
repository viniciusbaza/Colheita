using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;

namespace FarmAndFriends.Api.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<Seed> Seeds => Set<Seed>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<TheftLog> TheftLogs => Set<TheftLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CropCareCompletion> CropCareCompletions =>
        Set<CropCareCompletion>();
    public DbSet<VisitorFarmCareCycle> VisitorFarmCareCycles =>
        Set<VisitorFarmCareCycle>();
    public DbSet<PestRemovalCompletion> PestRemovalCompletions =>
        Set<PestRemovalCompletion>();
    public DbSet<LandPurchaseCompletion> LandPurchaseCompletions =>
        Set<LandPurchaseCompletion>();
    public DbSet<PremiumCurrencyTransaction> PremiumCurrencyTransactions =>
        Set<PremiumCurrencyTransaction>();
    public DbSet<PremiumCurrencyPurchaseItem> PremiumCurrencyPurchaseItems =>
        Set<PremiumCurrencyPurchaseItem>();

    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsurePremiumLedgerIsImmutable();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsurePremiumLedgerIsImmutable();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId);
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Level)
                .HasDefaultValue(1);

            entity.HasIndex(u => u.NormalizedUsername)
                .IsUnique();
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasIndex(f => new { f.UserAId, f.UserBId })
                .IsUnique();

            entity.HasIndex(f => new { f.Status, f.RequestedByUserId });

            entity.HasOne(f => f.UserA)
                .WithMany()
                .HasForeignKey(f => f.UserAId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.UserB)
                .WithMany()
                .HasForeignKey(f => f.UserBId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.RequestedByUser)
                .WithMany()
                .HasForeignKey(f => f.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Friendships_DifferentUsers",
                    "\"UserAId\" <> \"UserBId\"");

                table.HasCheckConstraint(
                    "CK_Friendships_RequesterIsParticipant",
                    "\"RequestedByUserId\" = \"UserAId\" OR \"RequestedByUserId\" = \"UserBId\"");
            });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => new { n.RecipientUserId, n.ReadAt, n.CreatedAt });
            entity.HasIndex(n => new { n.Type, n.ActorUserId, n.CreatedAt });
            entity.HasIndex(n => new
            {
                n.Type,
                n.ActorUserId,
                n.RecipientUserId,
                n.CreatedAt
            });
            entity.HasIndex(n => n.TheftLogId)
                .IsUnique();
            entity.HasIndex(n => n.CareOpportunityId)
                .IsUnique();
            entity.HasIndex(n => new { n.PestPlotId, n.CreatedAt });

            entity.HasOne(n => n.RecipientUser)
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.ActorUser)
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Friendship)
                .WithMany()
                .HasForeignKey(n => n.FriendshipId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(n => n.TheftLog)
                .WithMany()
                .HasForeignKey(n => n.TheftLogId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CropCareCompletion>(entity =>
        {
            entity.HasIndex(completion => new
                {
                    completion.VisitorUserId,
                    completion.CareOpportunityId,
                    completion.CaredAt
                });

            entity.HasIndex(completion => new
                {
                    completion.VisitorFarmCareCycleId,
                    completion.CareOpportunityId
                });

            entity.HasIndex(completion => new
            {
                completion.CareOpportunityId,
                completion.CaredAt
            });

            entity.HasIndex(completion => new
                {
                    completion.VisitorUserId,
                    completion.IdempotencyKey
                })
                .IsUnique();

            entity.HasIndex(completion => new
            {
                completion.VisitorUserId,
                completion.CaredAt
            });

            entity.HasIndex(completion => new
            {
                completion.VisitorUserId,
                completion.OwnerUserId,
                completion.CaredAt
            });

            entity.HasOne(completion => completion.VisitorUser)
                .WithMany()
                .HasForeignKey(completion => completion.VisitorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.OwnerUser)
                .WithMany()
                .HasForeignKey(completion => completion.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.VisitorFarmCareCycle)
                .WithMany(cycle => cycle.Completions)
                .HasForeignKey(completion =>
                    completion.VisitorFarmCareCycleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VisitorFarmCareCycle>(entity =>
        {
            entity.HasIndex(cycle => new
            {
                cycle.VisitorUserId,
                cycle.FarmId,
                cycle.StartedAt
            });

            entity.HasIndex(cycle => new
            {
                cycle.VisitorUserId,
                cycle.FarmId,
                cycle.RewardGranted,
                cycle.StartedAt
            }).HasDatabaseName(
                "IX_CareCycles_Visitor_Farm_Rewarded_StartedAt");

            entity.HasOne(cycle => cycle.VisitorUser)
                .WithMany()
                .HasForeignKey(cycle => cycle.VisitorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cycle => cycle.OwnerUser)
                .WithMany()
                .HasForeignKey(cycle => cycle.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cycle => cycle.Farm)
                .WithMany()
                .HasForeignKey(cycle => cycle.FarmId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_VisitorFarmCareCycles_DifferentUsers",
                    "\"VisitorUserId\" <> \"OwnerUserId\"");

                table.HasCheckConstraint(
                    "CK_VisitorFarmCareCycles_ValidWindow",
                    "\"EndsAt\" > \"StartedAt\"");
            });
        });

        modelBuilder.Entity<PestRemovalCompletion>(entity =>
        {
            entity.HasIndex(completion => completion.PestOccurrenceId)
                .IsUnique();

            entity.HasIndex(completion => new
                {
                    completion.ActorUserId,
                    completion.IdempotencyKey
                })
                .IsUnique();

            entity.HasIndex(completion => new
                {
                    completion.ActorUserId,
                    completion.RemovedAt
                });

            entity.HasOne(completion => completion.ActorUser)
                .WithMany()
                .HasForeignKey(completion => completion.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.OwnerUser)
                .WithMany()
                .HasForeignKey(completion => completion.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PestRemovalCompletions_CoinsGained_NonNegative",
                    "\"CoinsGained\" >= 0");
                table.HasCheckConstraint(
                    "CK_PestRemovalCompletions_XpGained_NonNegative",
                    "\"XpGained\" >= 0");
                table.HasCheckConstraint(
                    "CK_PestRemovalCompletions_CoinsAfter_NonNegative",
                    "\"CoinsAfter\" >= 0");
                table.HasCheckConstraint(
                    "CK_PestRemovalCompletions_RemainingYield_Positive",
                    "\"RemainingYieldAfter\" IS NULL OR \"RemainingYieldAfter\" >= 1");
            });
        });

        modelBuilder.Entity<LandPurchaseCompletion>(entity =>
        {
            entity.HasIndex(completion => new
                {
                    completion.BuyerUserId,
                    completion.IdempotencyKey
                })
                .IsUnique();

            entity.HasIndex(completion => completion.PlotId)
                .IsUnique();

            entity.HasIndex(completion => new
                {
                    completion.FarmId,
                    completion.PlotNumber
                })
                .IsUnique();

            entity.HasIndex(completion =>
                    completion.PremiumCurrencyTransactionId)
                .IsUnique();

            entity.HasOne(completion => completion.BuyerUser)
                .WithMany()
                .HasForeignKey(completion => completion.BuyerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.Farm)
                .WithMany()
                .HasForeignKey(completion => new
                {
                    completion.FarmId,
                    completion.BuyerUserId
                })
                .HasPrincipalKey(farm => new { farm.Id, farm.UserId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.Plot)
                .WithMany()
                .HasForeignKey(completion => new
                {
                    completion.PlotId,
                    completion.FarmId
                })
                .HasPrincipalKey(plot => new { plot.Id, plot.FarmId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion =>
                    completion.PremiumCurrencyTransaction)
                .WithMany()
                .HasForeignKey(completion =>
                    completion.PremiumCurrencyTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_PlotNumber_Range",
                    "\"PlotNumber\" >= 7 AND \"PlotNumber\" <= 28");
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_PaymentCurrency_Valid",
                    "\"PaymentCurrency\" IN ('coins', 'premiumCoins')");
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_AmountSpent_Positive",
                    "\"AmountSpent\" > 0");
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_CoinsAfter_NonNegative",
                    "\"CoinsAfter\" >= 0");
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_PremiumCoinsAfter_NonNegative",
                    "\"PremiumCoinsAfter\" >= 0");
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_ExpansionMetadata_Consistent",
                    "(\"PlotNumber\" = 9 AND \"AddedPlotCount\" = 19 AND \"ExpandedWidth\" = 7 AND \"ExpandedHeight\" = 4) OR (\"PlotNumber\" <> 9 AND \"AddedPlotCount\" = 0 AND \"ExpandedWidth\" IS NULL AND \"ExpandedHeight\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_LandPurchaseCompletions_PremiumLedger_Consistent",
                    "(\"PaymentCurrency\" = 'premiumCoins' AND \"PremiumCurrencyTransactionId\" IS NOT NULL) OR (\"PaymentCurrency\" = 'coins' AND \"PremiumCurrencyTransactionId\" IS NULL)");
            });
        });

        modelBuilder.Entity<PremiumCurrencyTransaction>(entity =>
        {
            entity.Property(transaction => transaction.LedgerSequence)
                .UseIdentityAlwaysColumn();
            entity.Property(transaction => transaction.EventType)
                .HasConversion(
                    eventType => eventType.ToToken(),
                    token => PremiumCurrencyEventTokens.Parse(token))
                .HasMaxLength(60);
            entity.Property(transaction => transaction.EventReference)
                .HasMaxLength(PremiumCurrencyService.MaximumEventReferenceLength);
            entity.Property(transaction => transaction.IdempotencyKey)
                .HasMaxLength(PremiumCurrencyService.MaximumIdempotencyKeyLength);
            entity.Property(transaction => transaction.OperationFingerprint)
                .HasMaxLength(67);
            entity.Property(transaction => transaction.ExternalSource)
                .HasMaxLength(PremiumCurrencyService.MaximumExternalReferenceLength);
            entity.Property(transaction => transaction.ExternalTransactionId)
                .HasMaxLength(PremiumCurrencyService.MaximumExternalReferenceLength);
            entity.Property(transaction => transaction.Metadata)
                .HasColumnType("jsonb");

            entity.HasIndex(transaction => transaction.LedgerSequence)
                .IsUnique();
            entity.HasIndex(transaction => new
                {
                    transaction.UserId,
                    transaction.LedgerSequence
                })
                .IsDescending(false, true);
            entity.HasIndex(transaction => new
                {
                    transaction.EventType,
                    transaction.EventReference
                });
            entity.HasIndex(transaction => new
                {
                    transaction.UserId,
                    transaction.IdempotencyKey
                })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.HasIndex(transaction => new
                {
                    transaction.ExternalSource,
                    transaction.ExternalTransactionId
                })
                .IsUnique()
                .HasFilter("\"ExternalSource\" IS NOT NULL AND \"ExternalTransactionId\" IS NOT NULL");
            entity.HasIndex(transaction => transaction.ReversesTransactionId)
                .IsUnique()
                .HasFilter("\"ReversesTransactionId\" IS NOT NULL");

            entity.HasOne(transaction => transaction.User)
                .WithMany()
                .HasForeignKey(transaction => transaction.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.ReversesTransaction)
                .WithMany()
                .HasForeignKey(transaction => transaction.ReversesTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyTransactions_Amount_NonZero",
                    "\"Amount\" <> 0");
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyTransactions_BalanceBefore_NonNegative",
                    "\"BalanceBefore\" >= 0");
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyTransactions_BalanceAfter_NonNegative",
                    "\"BalanceAfter\" >= 0");
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyTransactions_Balance_Consistent",
                    "\"BalanceAfter\" = \"BalanceBefore\" + \"Amount\"");
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyTransactions_ExternalReference_Complete",
                    "(\"ExternalSource\" IS NULL AND \"ExternalTransactionId\" IS NULL) OR (\"ExternalSource\" IS NOT NULL AND \"ExternalTransactionId\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyTransactions_IdempotencyReference_Present",
                    "\"IdempotencyKey\" IS NOT NULL OR \"ExternalSource\" IS NOT NULL");
            });
        });

        modelBuilder.Entity<PremiumCurrencyPurchaseItem>(entity =>
        {
            entity.Property(item => item.ItemIdSnapshot)
                .HasMaxLength(PremiumCurrencyService.MaximumItemIdentifierLength);
            entity.Property(item => item.ItemNameSnapshot)
                .HasMaxLength(PremiumCurrencyService.MaximumItemNameLength);

            entity.HasIndex(item => item.PremiumCurrencyTransactionId);
            entity.HasOne(item => item.PremiumCurrencyTransaction)
                .WithMany(transaction => transaction.PurchaseItems)
                .HasForeignKey(item => item.PremiumCurrencyTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyPurchaseItems_Quantity_Positive",
                    "\"Quantity\" > 0");
                table.HasCheckConstraint(
                    "CK_PremiumCurrencyPurchaseItems_UnitPrice_Positive",
                    "\"UnitPremiumPrice\" > 0");
            });
        });

        modelBuilder.Entity<Plot>(entity =>
        {
            entity.Ignore(p => p.IsReady);

            entity.HasIndex(p => new { p.FarmId, p.X, p.Y })
                .IsUnique();

            entity.HasIndex(p => p.CareOpportunityId)
                .IsUnique();

            entity.HasIndex(p => p.PestOccurrenceId);

            entity.Property(p => p.PestStatus)
                .HasDefaultValue(PestStatus.None);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Plots_RemainingYield_Positive",
                    "\"RemainingYield\" IS NULL OR \"RemainingYield\" >= 1");
                table.HasCheckConstraint(
                    "CK_Plots_PestConsumedAmount_NonNegative",
                    "\"PestConsumedAmount\" >= 0");
                table.HasCheckConstraint(
                    "CK_Plots_Coordinates_NonNegative",
                    "\"X\" >= 0 AND \"Y\" >= 0");
            });
        });

        modelBuilder.Entity<Seed>()
            .Property(s => s.GrowTime)
            .HasConversion(
                v => v.TotalSeconds,
                v => TimeSpan.FromSeconds(v)
            );

        modelBuilder.Entity<Seed>().HasData(
            new Seed
            {
                Id = "carrot",
                Name = "Cenoura",
                BuyPrice = 10,
                SellPrice = 20,
                Icon = "🥕",
                GrowTime = TimeSpan.FromMinutes(5),
                TheftChancePercent = 100,
                MinLevel = 1,   
                CropId = "carrot_crop",
                CropAmount = 1
            },
            new Seed
            {
                Id = "corn",
                Name = "Milho",
                Icon = "🌽",
                BuyPrice = 20,
                SellPrice = 45,
                GrowTime = TimeSpan.FromMinutes(2),
                TheftChancePercent = 100,
                MinLevel = 2,
                CropId = "corn_crop",
                CropAmount = 3  
            },
            new Seed
            {
                Id = "tomato",
                Name = "Tomate",
                Icon = "🍅",
                BuyPrice = 30,
                SellPrice = 60,
                GrowTime = TimeSpan.FromMinutes(2),
                TheftChancePercent = 100,
                MinLevel = 3,
                CropId = "tomato_crop",
                CropAmount = 4
            },
            new Seed
            {
                Id = "pumpkin",
                Name = "Abóbora",
                Icon = "🎃",
                BuyPrice = 40,
                SellPrice = 80,
                GrowTime = TimeSpan.FromMinutes(2),
                TheftChancePercent = 100,
                MinLevel = 4,
                CropId = "pumpkin_crop",
                CropAmount = 5
            }
        );

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.HasOne(i => i.Inventory)
                .WithMany(i => i.Items)
                .HasForeignKey(i => i.InventoryId);
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasIndex(inventory => inventory.UserId)
                .IsUnique();

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Inventories_Coins_NonNegative",
                    "\"Coins\" >= 0");
                table.HasCheckConstraint(
                    "CK_Inventories_PremiumCoins_NonNegative",
                    "\"PremiumCoins\" >= 0");
            });
        });

        modelBuilder.Entity<TheftLog>()
            .HasIndex(t => new { t.FarmId, t.ThiefUserId, t.CreatedAt });

        modelBuilder.Entity<TheftLog>()
            .HasOne(t => t.Seed)
            .WithMany()
            .HasForeignKey(t => t.SeedId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<TheftLog>()
            .HasOne(t => t.Plot)
            .WithMany()
            .HasForeignKey(t => t.PlotId)
            .OnDelete(DeleteBehavior.Cascade);

    }

    private void EnsurePremiumLedgerIsImmutable()
    {
        var mutableTransaction = ChangeTracker
            .Entries<PremiumCurrencyTransaction>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified
                or EntityState.Deleted);
        var mutableItem = ChangeTracker
            .Entries<PremiumCurrencyPurchaseItem>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified
                or EntityState.Deleted);

        if (mutableTransaction != null || mutableItem != null)
        {
            throw new InvalidOperationException(
                "Premium currency ledger records are immutable at the application level.");
        }
    }
}
