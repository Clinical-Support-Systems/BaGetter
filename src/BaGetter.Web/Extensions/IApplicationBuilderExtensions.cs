using System;
using Microsoft.AspNetCore.Builder;

namespace BaGetter.Web;

public static class IApplicationBuilderExtensions
{
    public static IApplicationBuilder UseOperationCancelledMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<OperationCancelledMiddleware>();
    }

    public static IApplicationBuilder UseFeedResolutionMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<FeedResolutionMiddleware>();
    }

    public static IApplicationBuilder UseFeedReadAuthenticationMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<FeedReadAuthenticationMiddleware>();
    }
}
