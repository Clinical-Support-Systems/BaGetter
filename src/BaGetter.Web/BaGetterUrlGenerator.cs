using System;
using BaGetter.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NuGet.Versioning;

namespace BaGetter.Web;

// TODO: This should validate the "Host" header against known valid values
public class BaGetterUrlGenerator : IUrlGenerator
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public BaGetterUrlGenerator(IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
    }

    public string GetServiceIndexUrl()
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.IndexRouteName, Routes.FeedIndexRouteName),
            values: FeedRouteValues());
    }

    public string GetPackageContentResourceUrl()
    {
        return AbsoluteUrl("v3/package/");
    }

    public string GetPackageMetadataResourceUrl()
    {
        return AbsoluteUrl("v3/registration/");
    }

    public string GetPackagePublishResourceUrl()
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.UploadPackageRouteName, Routes.FeedUploadPackageRouteName),
            values: FeedRouteValues());
    }

    public string GetSymbolPublishResourceUrl()
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.UploadSymbolRouteName, Routes.FeedUploadSymbolRouteName),
            values: FeedRouteValues());
    }

    public string GetSearchResourceUrl()
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.SearchRouteName, Routes.FeedSearchRouteName),
            values: FeedRouteValues());
    }

    public string GetAutocompleteResourceUrl()
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.AutocompleteRouteName, Routes.FeedAutocompleteRouteName),
            values: FeedRouteValues());
    }

    public string GetRegistrationIndexUrl(string id)
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.RegistrationIndexRouteName, Routes.FeedRegistrationIndexRouteName),
            values: BuildRouteValues(new { Id = id.ToLowerInvariant() }));
    }

    public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper)
    {
        // BaGetter does not support paging the registration resource.
        throw new NotImplementedException();
    }

    public string GetRegistrationLeafUrl(string id, NuGetVersion version)
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.RegistrationLeafRouteName, Routes.FeedRegistrationLeafRouteName),
            values: BuildRouteValues(new
            {
                Id = id.ToLowerInvariant(),
                Version = version.ToNormalizedString().ToLowerInvariant(),
            }));
    }

    public string GetPackageVersionsUrl(string id)
    {
        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.PackageVersionsRouteName, Routes.FeedPackageVersionsRouteName),
            values: BuildRouteValues(new { Id = id.ToLowerInvariant() }));
    }

    public string GetPackageDownloadUrl(string id, NuGetVersion version)
    {
        id = id.ToLowerInvariant();
        var versionString = version.ToNormalizedString().ToLowerInvariant();

        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.PackageDownloadRouteName, Routes.FeedPackageDownloadRouteName),
            values: BuildRouteValues(new
            {
                Id = id,
                Version = versionString,
                IdVersion = $"{id}.{versionString}"
            }));
    }

    public string GetPackageManifestDownloadUrl(string id, NuGetVersion version)
    {
        id = id.ToLowerInvariant();
        var versionString = version.ToNormalizedString().ToLowerInvariant();

        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.PackageDownloadManifestRouteName, Routes.FeedPackageDownloadManifestRouteName),
            values: BuildRouteValues(new
            {
                Id = id,
                Version = versionString,
                Id2 = id,
            }));
    }

    public string GetPackageIconDownloadUrl(string id, NuGetVersion version)
    {
        id = id.ToLowerInvariant();
        var versionString = version.ToNormalizedString().ToLowerInvariant();

        return _linkGenerator.GetUriByRouteValues(
            _httpContextAccessor.HttpContext,
            GetRouteName(Routes.PackageDownloadIconRouteName, Routes.FeedPackageDownloadIconRouteName),
            values: BuildRouteValues(new
            {
                Id = id,
                Version = versionString
            }));
    }

    private string AbsoluteUrl(string relativePath)
    {
        var request = _httpContextAccessor.HttpContext.Request;

        return string.Concat(
            request.Scheme,
            "://",
            request.Host.ToUriComponent(),
            request.PathBase.ToUriComponent(),
            "/",
            relativePath);
    }

    private string GetRouteName(string rootRouteName, string feedRouteName)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var feed = request?.RouteValues[FeedContext.RouteValueName]?.ToString();
        return string.IsNullOrWhiteSpace(feed) ? rootRouteName : feedRouteName;
    }

    private object FeedRouteValues()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var feed = request?.RouteValues[FeedContext.RouteValueName]?.ToString();
        return string.IsNullOrWhiteSpace(feed) ? null : new { feed };
    }

    private object BuildRouteValues(object values)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var feed = request?.RouteValues[FeedContext.RouteValueName]?.ToString();
        if (string.IsNullOrWhiteSpace(feed))
        {
            return values;
        }

        var dict = new RouteValueDictionary(values)
        {
            [FeedContext.RouteValueName] = feed,
        };
        return dict;
    }
}
