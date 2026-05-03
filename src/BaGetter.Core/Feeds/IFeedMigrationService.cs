using System.Threading;
using System.Threading.Tasks;

namespace BaGetter.Core;

public interface IFeedMigrationService
{
    Task<FeedMigrationResult> MigrateLegacyRootToFeedAsync(string targetFeed, bool dryRun, CancellationToken cancellationToken);
}

public sealed record FeedMigrationResult(
    int TableCopied,
    int TableSkipped,
    int BlobCopied,
    int BlobSkipped);
