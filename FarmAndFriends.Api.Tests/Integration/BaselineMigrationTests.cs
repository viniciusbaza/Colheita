using FarmAndFriends.Api.Infrastructure.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class BaselineMigrationTests
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task InitialCreate_BuildsCurrentSchemaFromEmptyDatabase()
    {
        var configuredConnection = Environment.GetEnvironmentVariable(
            "CROP_CARE_TEST_CONNECTION")!;
        var databaseName = $"baseline_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(
            configuredConnection)
        {
            Database = "postgres"
        }.ConnectionString;
        var databaseConnectionString = new NpgsqlConnectionStringBuilder(
            configuredConnection)
        {
            Database = databaseName
        }.ConnectionString;

        await using var adminConnection = new NpgsqlConnection(
            adminConnectionString);
        await adminConnection.OpenAsync();
        await using (var createDatabase = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName}\";",
            adminConnection))
        {
            await createDatabase.ExecuteNonQueryAsync();
        }

        try
        {
            await using var context = CreateContext(databaseConnectionString);
            await context.Database.MigrateAsync();

            await AssertAllMigrationsAppliedAsync(context);
            var designTimeModel = context.GetService<IDesignTimeModel>().Model;

            await using var databaseConnection = new NpgsqlConnection(
                databaseConnectionString);
            await databaseConnection.OpenAsync();

            await AssertTablesAndColumnsMatchModelAsync(
                designTimeModel,
                databaseConnection);
            await AssertConstraintsMatchModelAsync(
                designTimeModel,
                databaseConnection);
            await AssertIndexesMatchModelAsync(
                designTimeModel,
                databaseConnection);
            await AssertNoHistoricalDatabaseCodeAsync(databaseConnection);
            await AssertSeedCatalogAsync(context);
        }
        finally
        {
            await using var dropDatabase = new NpgsqlCommand(
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE);",
                adminConnection);
            await dropDatabase.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task MultiHarvestMigrations_BackfillExistingDataAndRepairTomatoCatalog()
    {
        var configuredConnection = Environment.GetEnvironmentVariable(
            "CROP_CARE_TEST_CONNECTION")!;
        var databaseName = $"multi_harvest_upgrade_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(
            configuredConnection)
        {
            Database = "postgres"
        }.ConnectionString;
        var databaseConnectionString = new NpgsqlConnectionStringBuilder(
            configuredConnection)
        {
            Database = databaseName
        }.ConnectionString;

        await using var adminConnection = new NpgsqlConnection(
            adminConnectionString);
        await adminConnection.OpenAsync();
        await using (var createDatabase = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName}\";",
            adminConnection))
        {
            await createDatabase.ExecuteNonQueryAsync();
        }

        try
        {
            var ownerId = Guid.NewGuid();
            var farmId = Guid.NewGuid();
            var occupiedPlotId = Guid.NewGuid();
            var emptyPlotId = Guid.NewGuid();
            var plantedAt = new DateTime(
                2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

            await using (var initialContext =
                         CreateContext(databaseConnectionString))
            {
                var migrator = initialContext.GetService<IMigrator>();
                await migrator.MigrateAsync(
                    "20260818050938_InitialCreate");

                await initialContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Users"
                        ("Id", "Username", "NormalizedUsername", "PasswordHash", "CurrentXp")
                    VALUES
                        ({ownerId}, 'upgrade_owner', 'UPGRADE_OWNER', 'test', 0);
                    """);
                await initialContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Farms" ("Id", "Name", "UserId")
                    VALUES ({farmId}, 'Upgrade farm', {ownerId});
                    """);
                await initialContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Plots"
                        ("Id", "FarmId", "X", "Y", "Unlocked", "SeedId",
                         "PlantedAt", "ReadyAt", "RemainingYield",
                         "PestConsumedAmount")
                    VALUES
                        ({occupiedPlotId}, {farmId}, 0, 0, TRUE, 'corn',
                         {plantedAt}, {plantedAt.AddMinutes(2)}, 3, 0),
                        ({emptyPlotId}, {farmId}, 1, 0, TRUE, NULL,
                         NULL, NULL, NULL, 0);
                    """);
                await initialContext.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "Seeds"
                        ("Id", "Name", "Icon", "BuyPrice", "SellPrice",
                         "GrowTime", "TheftChancePercent", "MinLevel",
                         "CropId", "CropAmount")
                    VALUES
                        ('legacy_custom', 'Legado', 'L', 1, 2,
                         60, 0, 1, 'legacy_crop', 1);
                    """);

                await migrator.MigrateAsync(
                    "20260824014226_MultiHarvestCrops");

                await AssertTomatoConfigurationAsync(
                    initialContext,
                    harvestCycles: 1,
                    regrowTime: null);

                await migrator.MigrateAsync();
            }

            await using var assertion = CreateContext(
                databaseConnectionString);
            var legacySeed = await assertion.Seeds
                .AsNoTracking()
                .SingleAsync(seed => seed.Id == "legacy_custom");
            var tomatoSeed = await assertion.Seeds
                .AsNoTracking()
                .SingleAsync(seed => seed.Id == "tomato");
            var occupiedPlot = await assertion.Plots
                .AsNoTracking()
                .SingleAsync(plot => plot.Id == occupiedPlotId);
            var emptyPlot = await assertion.Plots
                .AsNoTracking()
                .SingleAsync(plot => plot.Id == emptyPlotId);

            Assert.Equal("Legado", legacySeed.CropName);
            Assert.Equal(1, legacySeed.HarvestCycles);
            Assert.Null(legacySeed.RegrowTime);
            Assert.Equal("Tomate", tomatoSeed.CropName);
            Assert.Equal(2, tomatoSeed.HarvestCycles);
            Assert.Equal(TimeSpan.FromMinutes(2), tomatoSeed.RegrowTime);
            Assert.Equal(1, occupiedPlot.CurrentHarvestCycle);
            Assert.Equal(plantedAt, occupiedPlot.CurrentHarvestCycleStartedAt);
            Assert.Null(emptyPlot.CurrentHarvestCycle);
            Assert.Null(emptyPlot.CurrentHarvestCycleStartedAt);
            Assert.Empty(await assertion.Database
                .GetPendingMigrationsAsync());

            var emptyWithDeadline = await Assert.ThrowsAsync<PostgresException>(
                () => assertion.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE "Plots"
                    SET "ReadyAt" = NOW()
                    WHERE "Id" = {0};
                    """,
                    emptyPlotId));
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                emptyWithDeadline.SqlState);

            var occupiedWithoutDeadline =
                await Assert.ThrowsAsync<PostgresException>(
                    () => assertion.Database.ExecuteSqlRawAsync(
                        """
                        UPDATE "Plots"
                        SET "ReadyAt" = NULL
                        WHERE "Id" = {0};
                        """,
                        occupiedPlotId));
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                occupiedWithoutDeadline.SqlState);
        }
        finally
        {
            await using var dropDatabase = new NpgsqlCommand(
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE);",
                adminConnection);
            await dropDatabase.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task RepairTomatoMigration_DowngradesAndReappliesConsistently()
    {
        var configuredConnection = Environment.GetEnvironmentVariable(
            "CROP_CARE_TEST_CONNECTION")!;
        var databaseName = $"tomato_repair_cycle_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(
            configuredConnection)
        {
            Database = "postgres"
        }.ConnectionString;
        var databaseConnectionString = new NpgsqlConnectionStringBuilder(
            configuredConnection)
        {
            Database = databaseName
        }.ConnectionString;

        await using var adminConnection = new NpgsqlConnection(
            adminConnectionString);
        await adminConnection.OpenAsync();
        await using (var createDatabase = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName}\";",
            adminConnection))
        {
            await createDatabase.ExecuteNonQueryAsync();
        }

        try
        {
            await using var context = CreateContext(
                databaseConnectionString);
            var migrator = context.GetService<IMigrator>();

            await migrator.MigrateAsync();
            await AssertTomatoConfigurationAsync(
                context,
                harvestCycles: 2,
                regrowTime: TimeSpan.FromMinutes(2));
            await AssertAllMigrationsAppliedAsync(context);

            await migrator.MigrateAsync(
                "20260824014226_MultiHarvestCrops");
            await AssertTomatoConfigurationAsync(
                context,
                harvestCycles: 1,
                regrowTime: null);
            Assert.Equal(
                [
                    "20260818050938_InitialCreate",
                    "20260824014226_MultiHarvestCrops"
                ],
                (await context.Database.GetAppliedMigrationsAsync())
                .ToArray());
            Assert.Equal(
                context.Database.GetMigrations().Skip(2).ToArray(),
                (await context.Database.GetPendingMigrationsAsync())
                .ToArray());

            await migrator.MigrateAsync();
            await AssertTomatoConfigurationAsync(
                context,
                harvestCycles: 2,
                regrowTime: TimeSpan.FromMinutes(2));
            await AssertAllMigrationsAppliedAsync(context);
        }
        finally
        {
            await using var dropDatabase = new NpgsqlCommand(
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE);",
                adminConnection);
            await dropDatabase.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertTomatoConfigurationAsync(
        AppDbContext context,
        int harvestCycles,
        TimeSpan? regrowTime)
    {
        var tomato = await context.Seeds
            .AsNoTracking()
            .SingleAsync(seed => seed.Id == "tomato");

        Assert.Equal(harvestCycles, tomato.HarvestCycles);
        Assert.Equal(regrowTime, tomato.RegrowTime);
    }

    private static async Task AssertAllMigrationsAppliedAsync(
        AppDbContext context)
    {
        var availableMigrations = context.Database
            .GetMigrations()
            .ToArray();
        Assert.Equal(4, availableMigrations.Length);
        Assert.Equal(
            "20260818050938_InitialCreate",
            availableMigrations[0]);
        Assert.EndsWith("_MultiHarvestCrops", availableMigrations[1]);
        Assert.EndsWith(
            "_RepairTomatoMultiHarvestConfiguration",
            availableMigrations[2]);
        Assert.EndsWith("_AddUserAvatar", availableMigrations[3]);
        Assert.Equal(
            availableMigrations,
            (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static async Task AssertTablesAndColumnsMatchModelAsync(
        IModel model,
        NpgsqlConnection connection)
    {
        var relationalModel = model.GetRelationalModel();
        var expectedTables = relationalModel.Tables
            .Select(table => Signature(Schema(table.Schema), table.Name))
            .Append(Signature("public", "__EFMigrationsHistory"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualTables = await ReadSignaturesAsync(
            connection,
            """
            SELECT table_schema || '|' || table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE'
            ORDER BY 1;
            """);
        Assert.Equal(expectedTables, actualTables);

        var expectedColumns = relationalModel.Tables
            .SelectMany(table => table.Columns.Select(column =>
                ColumnSignature(table, column)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualColumns = await ReadSignaturesAsync(
            connection,
            """
            SELECT namespace.nspname || '|' || relation.relname || '|'
                || attribute.attname || '|'
                || format_type(attribute.atttypid, attribute.atttypmod) || '|'
                || CASE WHEN attribute.attnotnull
                    THEN 'required' ELSE 'nullable' END || '|'
                || COALESCE(
                    pg_get_expr(
                        default_value.adbin,
                        default_value.adrelid),
                    'none')
            FROM pg_attribute AS attribute
            JOIN pg_class AS relation
                ON relation.oid = attribute.attrelid
            JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            LEFT JOIN pg_attrdef AS default_value
                ON default_value.adrelid = attribute.attrelid
                AND default_value.adnum = attribute.attnum
            WHERE namespace.nspname = 'public'
                AND relation.relkind = 'r'
                AND relation.relname <> '__EFMigrationsHistory'
                AND attribute.attnum > 0
                AND NOT attribute.attisdropped
            ORDER BY 1;
            """);
        Assert.Equal(expectedColumns, actualColumns);
    }

    private static async Task AssertConstraintsMatchModelAsync(
        IModel model,
        NpgsqlConnection connection)
    {
        var entityTypes = model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .ToArray();

        var declaredChecks = entityTypes
            .SelectMany(entity => entity.GetCheckConstraints())
            .ToArray();
        var expectedChecks = await CanonicalizeCheckSignaturesAsync(
            connection,
            declaredChecks);
        var actualChecks = await ReadCheckSignaturesAsync(
            connection,
            """
            SELECT namespace.nspname,
                relation.relname,
                constraint_row.conname,
                constraint_row.convalidated,
                pg_get_expr(
                    constraint_row.conbin,
                    constraint_row.conrelid,
                    false)
            FROM pg_constraint AS constraint_row
            JOIN pg_class AS relation
                ON relation.oid = constraint_row.conrelid
            JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = 'public'
                AND constraint_row.contype = 'c'
                AND relation.relname <> '__EFMigrationsHistory'
            ORDER BY namespace.nspname,
                relation.relname,
                constraint_row.conname;
            """);
        Assert.True(
            expectedChecks.SequenceEqual(actualChecks),
            $"Expected checks:{Environment.NewLine}{string.Join(Environment.NewLine, expectedChecks)}{Environment.NewLine}Actual checks:{Environment.NewLine}{string.Join(Environment.NewLine, actualChecks)}");

        var expectedKeys = entityTypes
            .SelectMany(entity => entity.GetKeys())
            .Select(key => Signature(
                Schema(key.DeclaringEntityType.GetSchema()),
                key.DeclaringEntityType.GetTableName()!,
                key.GetName(),
                key.IsPrimaryKey() ? "primary" : "unique",
                Columns(key.Properties,
                    StoreObjectIdentifier.Table(
                        key.DeclaringEntityType.GetTableName()!,
                        key.DeclaringEntityType.GetSchema()))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualKeys = await ReadSignaturesAsync(
            connection,
            """
            SELECT namespace.nspname || '|' || relation.relname || '|'
                || constraint_row.conname || '|'
                || CASE constraint_row.contype
                    WHEN 'p' THEN 'primary' ELSE 'unique' END || '|'
                || key_columns.names
            FROM pg_constraint AS constraint_row
            JOIN pg_class AS relation
                ON relation.oid = constraint_row.conrelid
            JOIN pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            JOIN LATERAL (
                SELECT string_agg(attribute.attname, ',' ORDER BY key.ordinality)
                    AS names
                FROM unnest(constraint_row.conkey)
                    WITH ORDINALITY AS key(attribute_number, ordinality)
                JOIN pg_attribute AS attribute
                    ON attribute.attrelid = constraint_row.conrelid
                    AND attribute.attnum = key.attribute_number
            ) AS key_columns ON TRUE
            WHERE namespace.nspname = 'public'
                AND constraint_row.contype IN ('p', 'u')
                AND relation.relname <> '__EFMigrationsHistory'
            ORDER BY 1;
            """);
        Assert.Equal(expectedKeys, actualKeys);

        var expectedForeignKeys = entityTypes
            .SelectMany(entity => entity.GetForeignKeys())
            .Select(ForeignKeySignature)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualForeignKeys = await ReadSignaturesAsync(
            connection,
            """
            SELECT dependent_namespace.nspname || '|'
                || dependent_table.relname || '|'
                || constraint_row.conname || '|'
                || dependent_columns.names || '|'
                || principal_namespace.nspname || '|'
                || principal_table.relname || '|'
                || principal_columns.names || '|'
                || CASE constraint_row.confdeltype
                    WHEN 'a' THEN 'NO ACTION'
                    WHEN 'r' THEN 'RESTRICT'
                    WHEN 'c' THEN 'CASCADE'
                    WHEN 'n' THEN 'SET NULL'
                    WHEN 'd' THEN 'SET DEFAULT'
                  END
            FROM pg_constraint AS constraint_row
            JOIN pg_class AS dependent_table
                ON dependent_table.oid = constraint_row.conrelid
            JOIN pg_namespace AS dependent_namespace
                ON dependent_namespace.oid = dependent_table.relnamespace
            JOIN pg_class AS principal_table
                ON principal_table.oid = constraint_row.confrelid
            JOIN pg_namespace AS principal_namespace
                ON principal_namespace.oid = principal_table.relnamespace
            JOIN LATERAL (
                SELECT string_agg(attribute.attname, ',' ORDER BY key.ordinality)
                    AS names
                FROM unnest(constraint_row.conkey)
                    WITH ORDINALITY AS key(attribute_number, ordinality)
                JOIN pg_attribute AS attribute
                    ON attribute.attrelid = constraint_row.conrelid
                    AND attribute.attnum = key.attribute_number
            ) AS dependent_columns ON TRUE
            JOIN LATERAL (
                SELECT string_agg(attribute.attname, ',' ORDER BY key.ordinality)
                    AS names
                FROM unnest(constraint_row.confkey)
                    WITH ORDINALITY AS key(attribute_number, ordinality)
                JOIN pg_attribute AS attribute
                    ON attribute.attrelid = constraint_row.confrelid
                    AND attribute.attnum = key.attribute_number
            ) AS principal_columns ON TRUE
            WHERE dependent_namespace.nspname = 'public'
                AND constraint_row.contype = 'f'
            ORDER BY 1;
            """);
        Assert.Equal(expectedForeignKeys, actualForeignKeys);
    }

    private static async Task AssertIndexesMatchModelAsync(
        IModel model,
        NpgsqlConnection connection)
    {
        var expectedIndexes = model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .SelectMany(entity => entity.GetIndexes())
            .Select(index =>
            {
                var table = index.DeclaringEntityType.GetTableName()!;
                var schema = index.DeclaringEntityType.GetSchema();
                var storeObject = StoreObjectIdentifier.Table(table, schema);
                return Signature(
                    Schema(schema),
                    table,
                    index.GetDatabaseName(),
                    index.IsUnique ? "unique" : "non-unique",
                    Columns(index.Properties, storeObject));
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualIndexes = await ReadSignaturesAsync(
            connection,
            """
            SELECT namespace.nspname || '|' || table_row.relname || '|'
                || index_row.relname || '|'
                || CASE WHEN index_definition.indisunique
                    THEN 'unique' ELSE 'non-unique' END || '|'
                || index_columns.names
            FROM pg_index AS index_definition
            JOIN pg_class AS index_row
                ON index_row.oid = index_definition.indexrelid
            JOIN pg_class AS table_row
                ON table_row.oid = index_definition.indrelid
            JOIN pg_namespace AS namespace
                ON namespace.oid = table_row.relnamespace
            JOIN LATERAL (
                SELECT string_agg(attribute.attname, ',' ORDER BY key.ordinality)
                    AS names
                FROM unnest(index_definition.indkey::smallint[])
                    WITH ORDINALITY AS key(attribute_number, ordinality)
                JOIN pg_attribute AS attribute
                    ON attribute.attrelid = index_definition.indrelid
                    AND attribute.attnum = key.attribute_number
            ) AS index_columns ON TRUE
            WHERE namespace.nspname = 'public'
                AND table_row.relname <> '__EFMigrationsHistory'
                AND NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint AS constraint_row
                    WHERE constraint_row.conindid = index_definition.indexrelid)
            ORDER BY 1;
            """);
        Assert.Equal(expectedIndexes, actualIndexes);
    }

    private static async Task AssertNoHistoricalDatabaseCodeAsync(
        NpgsqlConnection connection)
    {
        Assert.Empty(await ReadSignaturesAsync(
            connection,
            """
            SELECT trigger_name
            FROM information_schema.triggers
            WHERE trigger_schema = 'public'
            ORDER BY trigger_name;
            """));
        Assert.Empty(await ReadSignaturesAsync(
            connection,
            """
            SELECT routine_name
            FROM information_schema.routines
            WHERE routine_schema = 'public'
            ORDER BY routine_name;
            """));
    }

    private static async Task AssertSeedCatalogAsync(AppDbContext context)
    {
        var actualSeeds = await context.Seeds
            .AsNoTracking()
            .OrderBy(seed => seed.Id)
            .Select(seed => new SeedCatalogEntry(
                seed.Id,
                seed.Name,
                seed.CropName,
                seed.Icon,
                seed.BuyPrice,
                seed.SellPrice,
                seed.GrowTime,
                seed.RegrowTime,
                seed.TheftChancePercent,
                seed.MinLevel,
                seed.CropId,
                seed.CropAmount,
                seed.HarvestCycles))
            .ToArrayAsync();
        Assert.Equal(ExpectedSeeds, actualSeeds);
    }

    private static string ColumnSignature(ITable table, IColumn column) =>
        Signature(
            Schema(table.Schema),
            table.Name,
            column.Name,
            column.StoreType,
            column.IsNullable ? "nullable" : "required",
            ColumnDefault(column));

    private static string ColumnDefault(IColumn column)
    {
        if (column.DefaultValueSql is not null)
            return NormalizeSql(column.DefaultValueSql);

        return column.DefaultValue switch
        {
            null => "none",
            string value => $"'{value.Replace("'", "''")}'::{Regex.Replace(column.StoreType, @"\(\d+\)", string.Empty)}",
            Enum value => Convert.ToInt64(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            bool value => value ? "true" : "false",
            IFormattable value => value.ToString(
                null,
                CultureInfo.InvariantCulture),
            var value => value.ToString()!
        };
    }

    private static async Task<string[]> CanonicalizeCheckSignaturesAsync(
        NpgsqlConnection connection,
        IReadOnlyList<ICheckConstraint> checks)
    {
        var signatures = new List<string>(checks.Count);
        // Let the same PostgreSQL version parse the model SQL, then roll the
        // temporary constraints back before inspecting the permanent schema.
        await using var transaction = await connection.BeginTransactionAsync();
        for (var index = 0; index < checks.Count; index++)
        {
            var check = checks[index];
            var schema = Schema(check.EntityType.GetSchema());
            var table = check.EntityType.GetTableName()!;
            var temporaryName = $"__baseline_expected_check_{index}";
            await using (var addExpectedCheck = new NpgsqlCommand(
                $"""
                ALTER TABLE {QuoteIdentifier(schema)}.{QuoteIdentifier(table)}
                ADD CONSTRAINT {QuoteIdentifier(temporaryName)}
                CHECK ({check.Sql}) NOT VALID;
                """,
                connection,
                transaction))
            {
                await addExpectedCheck.ExecuteNonQueryAsync();
            }

            await using var readCanonicalSql = new NpgsqlCommand(
                """
                SELECT pg_get_expr(
                    constraint_row.conbin,
                    constraint_row.conrelid,
                    false)
                FROM pg_constraint AS constraint_row
                WHERE constraint_row.conrelid =
                    to_regclass(@qualifiedTable)
                    AND constraint_row.conname = @constraintName;
                """,
                connection,
                transaction);
            readCanonicalSql.Parameters.AddWithValue(
                "qualifiedTable",
                $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}");
            readCanonicalSql.Parameters.AddWithValue(
                "constraintName",
                temporaryName);
            var canonicalSql = (string)(await readCanonicalSql
                .ExecuteScalarAsync())!;
            signatures.Add(Signature(
                schema,
                table,
                check.Name,
                "validated",
                NormalizeSql(canonicalSql)));
        }

        await transaction.RollbackAsync();
        return signatures
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeSql(string sql) =>
        Regex.Replace(
            sql,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static string ForeignKeySignature(IForeignKey foreignKey)
    {
        var dependentTable = foreignKey.DeclaringEntityType.GetTableName()!;
        var dependentSchema = foreignKey.DeclaringEntityType.GetSchema();
        var principalTable = foreignKey.PrincipalEntityType.GetTableName()!;
        var principalSchema = foreignKey.PrincipalEntityType.GetSchema();
        return Signature(
            Schema(dependentSchema),
            dependentTable,
            foreignKey.GetConstraintName(),
            Columns(
                foreignKey.Properties,
                StoreObjectIdentifier.Table(
                    dependentTable,
                    dependentSchema)),
            Schema(principalSchema),
            principalTable,
            Columns(
                foreignKey.PrincipalKey.Properties,
                StoreObjectIdentifier.Table(
                    principalTable,
                    principalSchema)),
            DeleteAction(foreignKey.DeleteBehavior));
    }

    private static string Columns(
        IReadOnlyList<IReadOnlyProperty> properties,
        StoreObjectIdentifier storeObject) =>
        string.Join(',', properties.Select(property =>
            property.GetColumnName(storeObject)));

    private static string DeleteAction(DeleteBehavior behavior) =>
        behavior switch
        {
            DeleteBehavior.Cascade => "CASCADE",
            DeleteBehavior.Restrict => "RESTRICT",
            DeleteBehavior.SetNull => "SET NULL",
            _ => "NO ACTION"
        };

    private static string Schema(string? schema) => schema ?? "public";

    private static string Signature(params object?[] values) =>
        string.Join('|', values);

    private static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private static async Task<string[]> ReadSignaturesAsync(
        NpgsqlConnection connection,
        string sql)
    {
        var signatures = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            signatures.Add(reader.GetString(0));

        return signatures.ToArray();
    }

    private static async Task<string[]> ReadCheckSignaturesAsync(
        NpgsqlConnection connection,
        string sql)
    {
        var signatures = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            signatures.Add(Signature(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3) ? "validated" : "not-validated",
                NormalizeSql(reader.GetString(4))));
        }

        return signatures.ToArray();
    }

    private static readonly SeedCatalogEntry[] ExpectedSeeds =
    [
        new("apple_tree", "Macieira", "Maçã", "🍎", 90, 30,
            TimeSpan.FromHours(2), TimeSpan.FromHours(1),
            100, 5, "apple_crop", 3, 3),
        new("carrot", "Cenoura", "Cenoura", "🥕", 10, 20,
            TimeSpan.FromMinutes(5), null,
            100, 1, "carrot_crop", 1, 1),
        new("corn", "Milho", "Milho", "🌽", 20, 45,
            TimeSpan.FromMinutes(2), null,
            100, 2, "corn_crop", 3, 1),
        new("pumpkin", "Abóbora", "Abóbora", "🎃", 40, 80,
            TimeSpan.FromMinutes(2), null,
            100, 4, "pumpkin_crop", 5, 1),
        new("tomato", "Tomate", "Tomate", "🍅", 30, 60,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2),
            100, 3, "tomato_crop", 4, 2)
    ];

    private sealed record SeedCatalogEntry(
        string Id,
        string Name,
        string CropName,
        string Icon,
        int BuyPrice,
        int SellPrice,
        TimeSpan GrowTime,
        TimeSpan? RegrowTime,
        int TheftChancePercent,
        int MinLevel,
        string CropId,
        int CropAmount,
        int HarvestCycles);
}
