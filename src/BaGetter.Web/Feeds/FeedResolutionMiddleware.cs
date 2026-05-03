using System;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.AspNetCore.Http;

namespace BaGetter.Web;

public sealed class FeedResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public FeedResolutionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, IFeedResolver resolver, IFeedContextAccessor accessor)
    {
        accessor.Current = resolver.Resolve(context);
        if (accessor.Current?.IsInvalid == true)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }
}
