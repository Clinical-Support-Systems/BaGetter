namespace BaGetter.Core;

public sealed class FeedContext
{
    public const string RouteValueName = "feed";

    public string Name { get; init; }

    public bool IsLegacySingleFeed { get; init; }

    public bool IsRootAlias { get; init; }

    public bool IsInvalid { get; init; }
}
