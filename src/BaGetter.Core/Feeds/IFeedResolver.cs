using Microsoft.AspNetCore.Http;

namespace BaGetter.Core;

public interface IFeedResolver
{
    FeedContext Resolve(HttpContext httpContext);
}

