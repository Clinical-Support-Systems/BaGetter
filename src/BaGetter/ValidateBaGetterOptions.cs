using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BaGetter.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace BaGetter;

/// <summary>
/// BaGetter's options configuration, specific to the default BaGetter application.
/// Don't use this if you are embedding BaGetter into your own custom ASP.NET Core application.
/// </summary>
public class ValidateBaGetterOptions
    : IValidateOptions<BaGetterOptions>
{
    private static readonly Regex FeedNameRegex = new("^[a-z][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedFeedNames
        = new(StringComparer.OrdinalIgnoreCase)
        {
            "api",
            "v2",
            "v3",
            "v3-flatcontainer",
            "package",
            "packages",
            "upload",
            "stats",
            "statistics",
            "health",
            "_content",
            "wwwroot",
            "css",
            "admin",
        };

    private static readonly HashSet<string> ValidDatabaseTypes
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AzureTable",
            "MySql",
            "PostgreSql",
            "Sqlite",
            "SqlServer",
        };

    private static readonly HashSet<string> ValidStorageTypes
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AliyunOss",
            "AwsS3",
            "AzureBlobStorage",
            "Filesystem",
            "GoogleCloud",
            "TencentCos",
            "Null"
        };

    private static readonly HashSet<string> ValidSearchTypes
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AzureSearch",
            "Database",
            "Null",
        };

    public ValidateOptionsResult Validate(string name, BaGetterOptions options)
    {
        var failures = new List<string>();

        if (options.Database == null) failures.Add($"The '{nameof(BaGetterOptions.Database)}' config is required");
        if (options.Mirror == null) failures.Add($"The '{nameof(BaGetterOptions.Mirror)}' config is required");
        if (options.Search == null) failures.Add($"The '{nameof(BaGetterOptions.Search)}' config is required");
        if (options.Storage == null) failures.Add($"The '{nameof(BaGetterOptions.Storage)}' config is required");

        if (!ValidDatabaseTypes.Contains(options.Database?.Type))
        {
            failures.Add(
                $"The '{nameof(BaGetterOptions.Database)}:{nameof(DatabaseOptions.Type)}' config is invalid. " +
                $"Allowed values: {string.Join(", ", ValidDatabaseTypes)}");
        }

        if (!ValidStorageTypes.Contains(options.Storage?.Type))
        {
            failures.Add(
                $"The '{nameof(BaGetterOptions.Storage)}:{nameof(StorageOptions.Type)}' config is invalid. " +
                $"Allowed values: {string.Join(", ", ValidStorageTypes)}");
        }

        if (!ValidSearchTypes.Contains(options.Search?.Type))
        {
            failures.Add(
                $"The '{nameof(BaGetterOptions.Search)}:{nameof(SearchOptions.Type)}' config is invalid. " +
                $"Allowed values: {string.Join(", ", ValidSearchTypes)}");
        }

        ValidateFeeds(options, failures);

        if (failures.Count != 0) return ValidateOptionsResult.Fail(failures);

        return ValidateOptionsResult.Success;
    }

    private static void ValidateFeeds(BaGetterOptions options, List<string> failures)
    {
        if (!FeedUtility.IsMultiFeedConfigured(options))
        {
            return;
        }

        // Multi-feed support works with any storage/database provider.
        // Feed-aware path prefixing is handled by PackageStorageService.

        if (options.Feeds == null || options.Feeds.Count == 0)
        {
            failures.Add("The 'Feeds' configuration cannot be empty when multi-feed mode is enabled.");
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feed in options.Feeds)
        {
            if (string.IsNullOrWhiteSpace(feed.Name))
            {
                failures.Add("Each feed must define a non-empty 'Name'.");
                continue;
            }

            if (!FeedNameRegex.IsMatch(feed.Name))
            {
                failures.Add($"Feed '{feed.Name}' has an invalid name. Names must match regex '{FeedNameRegex}'.");
            }

            if (ReservedFeedNames.Contains(feed.Name))
            {
                failures.Add($"Feed '{feed.Name}' is a reserved name.");
            }

            if (!names.Add(feed.Name))
            {
                failures.Add($"Duplicate feed name '{feed.Name}' was configured.");
            }

            if (feed.Name.Equals("licensed", StringComparison.OrdinalIgnoreCase) && !feed.RequireReadAuthentication)
            {
                failures.Add("The 'licensed' feed must enable read authentication before use.");
            }
        }

        var defaultFeed = options.FeedRouting?.DefaultFeed ?? "internal";
        if (!names.Contains(defaultFeed))
        {
            failures.Add($"The default feed '{defaultFeed}' is not present in 'Feeds'.");
        }

        var hasGlobalReadCredentials = options.Authentication?.Credentials is { Length: > 0 };
        if (options.Feeds.Any(f => f.RequireReadAuthentication && f.ApiKeys.Count == 0 && !hasGlobalReadCredentials))
        {
            failures.Add("Feeds that require read authentication must define authentication tokens/keys.");
        }
    }
}
