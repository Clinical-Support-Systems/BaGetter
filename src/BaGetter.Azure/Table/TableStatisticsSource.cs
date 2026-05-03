using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using BaGetter.Core;
using BaGetter.Core.Statistics;
using Microsoft.Extensions.Options;

namespace BaGetter.Azure;

    public class TableStatisticsSource : IStatisticsSource
{
    private readonly TableClient _table;
    private readonly IFeedContextAccessor _feed;
    private readonly IOptionsSnapshot<BaGetterOptions> _root;

    public TableStatisticsSource(
        TableServiceClient client,
        IOptions<AzureTableOptions> options,
        IFeedContextAccessor feed,
        IOptionsSnapshot<BaGetterOptions> root)
    {
        _table = client.GetTableClient(options.Value.TableName);
        _feed = feed;
        _root = root;
    }

    public async Task<StatisticsTotals> GetTotalsAsync()
    {
        var packageIds = new HashSet<string>();
        var versionsTotalAmount = 0;
        var filter = BuildFeedFilter();
        var query = string.IsNullOrEmpty(filter)
            ? _table.QueryAsync<TableEntity>(select: ["PartitionKey"])
            : _table.QueryAsync<TableEntity>(filter, select: ["PartitionKey"]);

        await foreach (var entity in query)
        {
            versionsTotalAmount++;

            if (!string.IsNullOrEmpty(entity.PartitionKey))
            {
                packageIds.Add(entity.PartitionKey);
            }
        }

        return new StatisticsTotals(packageIds.Count, versionsTotalAmount);
    }

    private string BuildFeedFilter()
    {
        var feed = _feed.Current;
        if (feed == null || feed.IsLegacySingleFeed || string.IsNullOrWhiteSpace(feed.Name))
        {
            return string.Empty;
        }

        var prefix = feed.Name;
        if (FeedUtility.TryFindFeed(_root.Value, feed.Name, out var configuredFeed)
            && !string.IsNullOrWhiteSpace(configuredFeed.Database?.PartitionPrefix))
        {
            prefix = configuredFeed.Database.PartitionPrefix;
        }

        var lower = $"{prefix.ToLowerInvariant()}|";
        var upper = $"{prefix.ToLowerInvariant()}|~";
        return $"PartitionKey ge '{lower}' and PartitionKey lt '{upper}'";
    }
}
