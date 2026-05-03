using System;
using System.Collections.Generic;
using System.Linq;
using BaGetter.Core.Configuration;

namespace BaGetter.Core;

public static class FeedUtility
{
    public static bool IsMultiFeedConfigured(BaGetterOptions options)
    {
        return options?.Feeds is { Count: > 0 };
    }

    public static FeedOptions GetDefaultFeed(BaGetterOptions options)
    {
        if (!IsMultiFeedConfigured(options))
        {
            return null;
        }

        var defaultFeedName = options.FeedRouting?.DefaultFeed ?? "internal";
        return options.Feeds.FirstOrDefault(f =>
            f.Name.Equals(defaultFeedName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryFindFeed(BaGetterOptions options, string feedName, out FeedOptions feed)
    {
        feed = null;

        if (!IsMultiFeedConfigured(options) || string.IsNullOrWhiteSpace(feedName))
        {
            return false;
        }

        feed = options.Feeds.FirstOrDefault(f =>
            f.Name.Equals(feedName, StringComparison.OrdinalIgnoreCase));
        return feed != null;
    }

    public static Dictionary<string, FeedOptions> BuildLookup(BaGetterOptions options)
    {
        if (!IsMultiFeedConfigured(options))
        {
            return new Dictionary<string, FeedOptions>(StringComparer.OrdinalIgnoreCase);
        }

        return options.Feeds.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
    }
}
