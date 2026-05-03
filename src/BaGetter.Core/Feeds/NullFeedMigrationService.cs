using System;
using System.Threading;
using System.Threading.Tasks;

namespace BaGetter.Core;

public sealed class NullFeedMigrationService : IFeedMigrationService
{
    public Task<FeedMigrationResult> MigrateLegacyRootToFeedAsync(string targetFeed, bool dryRun, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Feed migration is only available when Azure providers are configured.");
    }
}
