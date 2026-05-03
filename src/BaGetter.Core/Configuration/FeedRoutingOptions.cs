namespace BaGetter.Core.Configuration;

public class FeedRoutingOptions
{
    public string DefaultFeed { get; set; } = "internal";

    public FeedRoutingBehavior RootFeedBehavior { get; set; } = FeedRoutingBehavior.Alias;
}
