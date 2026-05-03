using System;
using System.Collections.Generic;

namespace BaGetter.Core.Configuration;

public class FeedOptions
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public bool IsReadOnly { get; set; }

    public bool IsMirror { get; set; }

    public PackageDeletionBehavior? PackageDeletionBehavior { get; set; }

    public string UpstreamPackageSource { get; set; }

    public bool RequireReadAuthentication { get; set; }

    public bool RequirePushAuthentication { get; set; } = true;

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public FeedStorageOptions Storage { get; set; } = new();

    public FeedDatabaseOptions Database { get; set; } = new();

    public List<FeedApiKeyOptions> ApiKeys { get; set; } = [];
}
