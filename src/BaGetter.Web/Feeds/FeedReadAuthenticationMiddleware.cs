using System;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BaGetter.Web;

public sealed class FeedReadAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public FeedReadAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, IOptionsSnapshot<BaGetterOptions> options, IFeedContextAccessor feed)
    {
        if (context.GetEndpoint() == null)
        {
            await _next(context);
            return;
        }

        if (NeedsReadAuthentication(context, options.Value, feed.Current))
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }
        }

        await _next(context);
    }

    private static bool NeedsReadAuthentication(HttpContext context, BaGetterOptions options, FeedContext feed)
    {
        if (feed == null || feed.IsLegacySingleFeed || string.IsNullOrWhiteSpace(feed.Name))
        {
            return false;
        }

        if (!FeedUtility.TryFindFeed(options, feed.Name, out var feedOptions))
        {
            return false;
        }

        if (!feedOptions.RequireReadAuthentication)
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
