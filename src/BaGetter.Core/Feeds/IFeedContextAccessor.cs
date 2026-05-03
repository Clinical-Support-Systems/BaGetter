namespace BaGetter.Core;

public interface IFeedContextAccessor
{
    FeedContext Current { get; set; }
}
