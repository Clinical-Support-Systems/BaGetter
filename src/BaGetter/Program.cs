using System;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BaGetter;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = CreateBuilder(args);
        var startup = new Startup(builder.Configuration);
        startup.ConfigureServices(builder.Services);
        builder.AddServiceDefaults();
        var host = builder.Build();
        startup.Configure(host, host.Environment);
        if (!host.ValidateStartupOptions())
        {
            return;
        }

        var app = new CommandLineApplication
        {
            Name = "baget",
            Description = "A light-weight NuGet service",
        };

        app.HelpOption(inherited: true);

        app.Command("import", import =>
        {
            import.Command("downloads", downloads =>
            {
                downloads.OnExecuteAsync(async cancellationToken =>
                {
                    using var scope = host.Services.CreateScope();
                    var importer = scope.ServiceProvider.GetRequiredService<DownloadsImporter>();

                    await importer.ImportAsync(cancellationToken);
                });
            });

            import.Command("feeds", feeds =>
            {
                var to = feeds.Option("--to <FEED>", "Target feed name (default: internal)", CommandOptionType.SingleValue);
                var execute = feeds.Option("--execute", "Execute changes. Without this option, runs a dry-run only.", CommandOptionType.NoValue);

                feeds.OnExecuteAsync(async cancellationToken =>
                {
                    using var scope = host.Services.CreateScope();
                    var migrator = scope.ServiceProvider.GetRequiredService<IFeedMigrationService>();
                    var targetFeed = to.HasValue() ? to.Value() : "internal";
                    var dryRun = !execute.HasValue();

                    var result = await migrator.MigrateLegacyRootToFeedAsync(targetFeed, dryRun, cancellationToken);
                    Console.WriteLine(
                        $"Feed migration dryRun={dryRun} tableCopied={result.TableCopied} tableSkipped={result.TableSkipped} blobCopied={result.BlobCopied} blobSkipped={result.BlobSkipped}");
                });
            });
        });

        app.Option("--urls", "The URLs that BaGetter should bind to.", CommandOptionType.SingleValue);

        app.OnExecuteAsync(async cancellationToken =>
        {
            await host.RunMigrationsAsync(cancellationToken);
            await host.RunAsync(cancellationToken);
        });

        await app.ExecuteAsync(args);
    }

    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

        if (!string.IsNullOrEmpty(root))
        {
            builder.Configuration.SetBasePath(root);
        }

        // Optionally load secrets from files in the conventional path
        builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

        builder.WebHost.ConfigureKestrel(options =>
        {
            // Remove the upload limit from Kestrel. If needed, an upload limit can
            // be enforced by a reverse proxy server, like IIS.
            options.Limits.MaxRequestBodySize = null;
        });

        return builder;
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, config) =>
            {
                var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

                if (!string.IsNullOrEmpty(root))
                {
                    config.SetBasePath(root);
                }

                config.AddKeyPerFile("/run/secrets", optional: true);
            })
            .ConfigureWebHostDefaults(web =>
            {
                web.ConfigureKestrel(options =>
                {
                    options.Limits.MaxRequestBodySize = null;
                });

                web.UseStartup<Startup>();
            });
    }
}
