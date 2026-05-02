using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using BaGetter.Core.Statistics;
using Microsoft.Extensions.Options;

namespace BaGetter.Azure;

public class TableStatisticsSource : IStatisticsSource
{
    private readonly TableClient _table;

    public TableStatisticsSource(
        TableServiceClient client,
        IOptions<AzureTableOptions> options)
    {
        _table = client.GetTableClient(options.Value.TableName);
    }

    public async Task<StatisticsTotals> GetTotalsAsync()
    {
        var packageIds = new HashSet<string>();
        var versionsTotalAmount = 0;
        var query = _table.QueryAsync<TableEntity>(select: ["PartitionKey"]);

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
}
