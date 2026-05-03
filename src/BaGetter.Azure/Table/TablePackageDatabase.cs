using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using BaGetter.Core;
using BaGetter.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGetter.Azure
{
    /// <summary>
    /// Stores the metadata of packages using Azure Table Storage.
    /// </summary>
    public class TablePackageDatabase : IPackageDatabase
    {
        private const int MaxPreconditionFailures = 5;

        private readonly TableClient _table;
        private readonly ILogger<TablePackageDatabase> _logger;
        private readonly IFeedContextAccessor _feed;
        private readonly IOptionsSnapshot<BaGetterOptions> _root;

        public TablePackageDatabase(
            TableServiceClient client,
            ILogger<TablePackageDatabase> logger,
            IOptions<AzureTableOptions> options,
            IFeedContextAccessor feed,
            IOptionsSnapshot<BaGetterOptions> root)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(logger);

            _table = client.GetTableClient(options.Value.TableName);
            _logger = logger;
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public async Task<PackageAddResult> AddAsync(Package package, CancellationToken cancellationToken)
        {
            try
            {
                var entity = TableOperationBuilder.GetEntity(package, _feed.Current ?? new FeedContext { IsLegacySingleFeed = true }, _root.Value);

                await _table.AddEntityAsync(entity, cancellationToken);
            }
            catch (RequestFailedException e) when (e.IsAlreadyExistsException())
            {
                return PackageAddResult.PackageAlreadyExists;
            }

            return PackageAddResult.Success;
        }

        public async Task AddDownloadAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken)
        {
            var partitionKey = GetPartitionKey(id);
            var rowKey = TableOperationBuilder.GetRowKey(version);
            var attempt = 0;

            while (true)
            {
                try
                {
                    var result = await _table.GetEntityIfExistsAsync<PackageDownloadsEntity>(
                        partitionKey,
                        rowKey,
                        cancellationToken: cancellationToken);

                    if (!result.HasValue)
                    {
                        return;
                    }

                    var entity = result.Value;

                    entity.Downloads += 1;

                    var updateResponse = await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge, cancellationToken);

                    // Not sure if there's gonna be an exception here so check both ways just in case
                    if(updateResponse.Status == (int?)HttpStatusCode.PreconditionFailed && attempt < MaxPreconditionFailures)
                    {
                        attempt++;
                        _logger.LogWarning(
                            "Retrying due to precondition failure, attempt {Attempt} of {MaxPreconditionFailures}",
                            attempt, MaxPreconditionFailures);
                        continue;
                    }

                    return;
                }
                catch (RequestFailedException e)
                    when (attempt < MaxPreconditionFailures && e.IsPreconditionFailedException())
                {
                    attempt++;
                    _logger.LogWarning(
                        e,
                        "Retrying due to precondition failure, attempt {Attempt} of {MaxPreconditionFailures}",
                        attempt, MaxPreconditionFailures);
                }
            }
        }

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            var partitionKey = GetPartitionKey(id);
            var filter = TableClient.CreateQueryFilter<PackageEntity>(p => p.PartitionKey == partitionKey);
            var query = _table.QueryAsync<PackageEntity>(
                filter,
                maxPerPage: 1,
                select: MinimalColumnSet,
                cancellationToken: cancellationToken);

            await foreach(var _ in query)
            {
                return true;
            }

            return false;
        }

        public async Task<bool> ExistsAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken)
        {
            var partitionKey = GetPartitionKey(id);
            var rowKey = TableOperationBuilder.GetRowKey(version);

            try
            {
                await _table.GetEntityAsync<PackageEntity>(
                    partitionKey,
                    rowKey,
                    MinimalColumnSet,
                    cancellationToken);

                return true;
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<Package>> FindAsync(string id, bool includeUnlisted, CancellationToken cancellationToken)
        {
            const int maxPerPage = 500;
            var partitionKey = GetPartitionKey(id);
            var partitionFilter = TableClient.CreateQueryFilter<PackageEntity>(p => p.PartitionKey == partitionKey);
            var filter = includeUnlisted
                ? partitionFilter
                : $"{partitionFilter} and ({TableClient.CreateQueryFilter<PackageEntity>(p => p.Listed == true)})";
            var query = _table.QueryAsync<PackageEntity>(filter, maxPerPage, cancellationToken: cancellationToken);

            var results = new List<Package>();
            await foreach (var entity in query)
            {
                results.Add(entity.AsPackage());
            }

            return results.OrderBy(p => p.Version).ToList();
        }

        public async Task<Package> FindOrNullAsync(
            string id,
            NuGetVersion version,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            var result = await _table.GetEntityIfExistsAsync<PackageEntity>(
                GetPartitionKey(id),
                TableOperationBuilder.GetRowKey(version),
                cancellationToken: cancellationToken);

            if (!result.HasValue)
            {
                return null;
            }

            var entity = result.Value;

            // Filter out the package if it's unlisted.
            if (!includeUnlisted && !entity.Listed)
            {
                return null;
            }

            return entity.AsPackage();
        }

        public async Task<bool> HardDeletePackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var result = await _table.DeleteEntityAsync(
                GetPartitionKey(id),
                TableOperationBuilder.GetRowKey(version),
                cancellationToken: cancellationToken);
            return !result.IsError;
        }

        public async Task<bool> RelistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var result = await _table.GetEntityIfExistsAsync<PackageListingEntity>(
                GetPartitionKey(id),
                TableOperationBuilder.GetRowKey(version),
                cancellationToken: cancellationToken);

            if (!result.HasValue)
            {
                return false;
            }

            var entity = result.Value;

            entity.Listed = true;

            await _table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, cancellationToken);

            return true;
        }

        public async Task<bool> UnlistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var result = await _table.GetEntityIfExistsAsync<PackageListingEntity>(
                GetPartitionKey(id),
                TableOperationBuilder.GetRowKey(version),
                cancellationToken: cancellationToken);

            if (!result.HasValue)
            {
                return false;
            }

            var entity = result.Value;

            entity.Listed = false;

            await _table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, cancellationToken);

            return true;
        }

        private static List<string> MinimalColumnSet => ["PartitionKey"];

        private string GetPartitionKey(string id)
        {
            var feed = _feed.Current;
            var options = _root.Value;
            if (feed == null || feed.IsLegacySingleFeed || string.IsNullOrWhiteSpace(feed.Name))
            {
                return TableOperationBuilder.GetPartitionKey(id);
            }

            var partitionPrefix = feed.Name;
            if (FeedUtility.TryFindFeed(options, feed.Name, out var configuredFeed)
                && !string.IsNullOrWhiteSpace(configuredFeed.Database?.PartitionPrefix))
            {
                partitionPrefix = configuredFeed.Database.PartitionPrefix;
            }

            return TableOperationBuilder.GetPartitionKey(partitionPrefix, id);
        }
    }
}
