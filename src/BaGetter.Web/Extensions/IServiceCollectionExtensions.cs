using System;
using System.Text.Json.Serialization;
using BaGetter.Authentication;
using BaGetter.Core;
using BaGetter.Core.Statistics;
using BaGetter.Web;
using BaGetter.Web.Authentication;
using BaGetter.Web.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BaGetter;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddBaGetterWebApplication(
        this IServiceCollection services,
        Action<BaGetterApplication> configureAction)
    {
        services
            .AddRouting(options => options.LowercaseUrls = true)
            .AddControllers()
            .AddApplicationPart(typeof(PackageContentController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddRazorPages(options =>
        {
            options.Conventions.AddPageRoute("/Index", "{feed}");
            options.Conventions.AddPageRoute("/Index", "packages");
            options.Conventions.AddPageRoute("/Index", "{feed}/packages");
            options.Conventions.AddPageRoute("/Package", "{feed}/packages/{id}/{version?}");
            options.Conventions.AddPageRoute("/Upload", "{feed}/upload");
            options.Conventions.AddPageRoute("/Statistics", "{feed}/stats");
        });

        services.AddHttpContextAccessor();
        services.AddTransient<IUrlGenerator, BaGetterUrlGenerator>();
        services.AddScoped<IFeedContextAccessor, FeedContextAccessor>();
        services.AddScoped<IFeedResolver, RequestFeedResolver>();

        services.AddSingleton(ApplicationVersionHelper.GetVersion());

        services.AddBaGetterApplication(configureAction);

        // Resolve the active provider-backed services automatically for web hosts.
        services.AddScoped(DependencyInjectionExtensions.GetServiceFromProviders<IContext>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IPackageDatabase>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStatisticsSource>);
        services.AddTransient<IFeedMigrationService>(provider =>
            DependencyInjectionExtensions.GetServiceFromProviders<IFeedMigrationService>(provider)
            ?? provider.GetRequiredService<NullFeedMigrationService>());

        return services;
    }
}
