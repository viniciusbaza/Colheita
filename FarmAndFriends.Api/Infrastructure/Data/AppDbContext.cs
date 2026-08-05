using Microsoft.EntityFrameworkCore;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;

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

        modelBuilder.Entity<Plot>(entity =>
        {
            entity.Ignore(p => p.IsReady);

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
}
