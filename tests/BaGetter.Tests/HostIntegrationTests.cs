using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Core.Statistics;
using BaGetter.Database.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGetter.Tests;

public class HostIntegrationTests
{
    private readonly string DatabaseTypeKey = "Database:Type";
    private readonly string ConnectionStringKey = "Database:ConnectionString";

    [Fact]
    public void ThrowsIfDatabaseTypeInvalid()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { DatabaseTypeKey, "InvalidType" }
        });

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IContext>());
    }

    [Fact]
    public void ReturnsDatabaseContext()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { DatabaseTypeKey, "Sqlite" },
            { ConnectionStringKey, "..." }
        });

        Assert.NotNull(provider.GetRequiredService<IContext>());
    }

    [Fact]
    public void ReturnsSqliteContext()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { DatabaseTypeKey, "Sqlite" },
            { ConnectionStringKey, "..." }
        });

        Assert.NotNull(provider.GetRequiredService<SqliteContext>());
    }

    [Fact]
    public void DefaultsToSqlite()
    {
        var provider = BuildServiceProvider();

        var context = provider.GetRequiredService<IContext>();

        Assert.IsType<SqliteContext>(context);
    }

    [Fact]
    public void AddBaGetterWebApplicationResolvesDatabaseContext()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "BaGetterTests",
            Guid.NewGuid().ToString("N"));
        var sqlitePath = Path.Combine(tempPath, "BaGetter.db");
        var storagePath = Path.Combine(tempPath, "Packages");

        Directory.CreateDirectory(tempPath);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { DatabaseTypeKey, "Sqlite" },
                { ConnectionStringKey, $"Data Source={sqlitePath}" },
                { "Storage:Type", "FileSystem" },
                { "Storage:Path", storagePath },
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddBaGetterWebApplication(bagetter =>
        {
            bagetter.AddSqliteDatabase();
            bagetter.AddFileStorage();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<SqliteContext>(scope.ServiceProvider.GetRequiredService<IContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStatisticsSource>());
    }

    [Fact]
    public async Task AddBaGetterWebApplicationReturnsStatisticsTotals()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "BaGetterTests",
            Guid.NewGuid().ToString("N"));
        var sqlitePath = Path.Combine(tempPath, "BaGetter.db");
        var storagePath = Path.Combine(tempPath, "Packages");

        Directory.CreateDirectory(tempPath);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { DatabaseTypeKey, "Sqlite" },
                { ConnectionStringKey, $"Data Source={sqlitePath}" },
                { "Storage:Type", "FileSystem" },
                { "Storage:Path", storagePath },
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddBaGetterWebApplication(bagetter =>
        {
            bagetter.AddSqliteDatabase();
            bagetter.AddFileStorage();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        await context.RunMigrationsAsync(default);
        context.Packages.Add(new Package { Id = "A", NormalizedVersionString = "1.0.0" });
        context.Packages.Add(new Package { Id = "A", NormalizedVersionString = "2.0.0" });
        context.Packages.Add(new Package { Id = "B", NormalizedVersionString = "1.0.0" });
        await context.SaveChangesAsync(default);

        var statistics = scope.ServiceProvider.GetRequiredService<IStatisticsService>();

        Assert.Equal(2, await statistics.GetPackagesTotalAmount());
        Assert.Equal(3, await statistics.GetVersionsTotalAmount());
    }

    private IServiceProvider BuildServiceProvider(Dictionary<string, string> configs = null)
    {
        var host = Program
            .CreateHostBuilder(Array.Empty<string>())
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(configs ?? new Dictionary<string, string>());
            })
            .Build();

        return host.Services;
    }
}
