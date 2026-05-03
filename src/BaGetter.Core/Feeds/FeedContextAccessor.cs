namespace BaGetter.Core;

public sealed class FeedContextAccessor : IFeedContextAccessor
{
    public FeedContext Current { get; set; }
}
