using System;
using BaGetter.Core;
using BaGetter.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace BaGetter.Web;

public sealed class RequestFeedResolver : IFeedResolver
{
    private readonly IOptionsSnapshot<BaGetterOptions> _options;

    public RequestFeedResolver(IOptionsSnapshot<BaGetterOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public FeedContext Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var options = _options.Value;
        if (!FeedUtility.IsMultiFeedConfigured(options))
        {
            return new FeedContext
            {
                Name = null,
                IsLegacySingleFeed = true,
                IsRootAlias = false,
            };
        }

        var routeFeed = httpContext.GetRouteValue(FeedContext.RouteValueName)?.ToString();
        if (FeedUtility.TryFindFeed(options, routeFeed, out var feed))
        {
            return new FeedContext
            {
                Name = feed.Name,
                IsLegacySingleFeed = false,
                IsRootAlias = false,
                IsInvalid = false,
            };
        }

        if (!string.IsNullOrWhiteSpace(routeFeed))
        {
            return new FeedContext
            {
                Name = null,
                IsLegacySingleFeed = false,
                IsRootAlias = false,
                IsInvalid = true,
            };
        }

        var defaultFeed = FeedUtility.GetDefaultFeed(options)?.Name ?? options.FeedRouting?.DefaultFeed ?? "internal";
        return new FeedContext
        {
            Name = defaultFeed,
            IsLegacySingleFeed = false,
            IsRootAlias = true,
            IsInvalid = false,
        };
    }
}
