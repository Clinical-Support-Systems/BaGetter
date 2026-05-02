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

        services.AddRazorPages();

        services.AddHttpContextAccessor();
        services.AddTransient<IUrlGenerator, BaGetterUrlGenerator>();

        services.AddSingleton(ApplicationVersionHelper.GetVersion());

        services.AddBaGetterApplication(configureAction);

        // Resolve the active provider-backed services automatically for web hosts.
        services.AddScoped(DependencyInjectionExtensions.GetServiceFromProviders<IContext>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IPackageDatabase>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStatisticsSource>);

        return services;
    }
}
