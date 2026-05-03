using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using BaGetter.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGetter.Azure;

public sealed class AzureFeedMigrationService : IFeedMigrationService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IOptionsSnapshot<AzureTableOptions> _tableOptions;
    private readonly IOptionsSnapshot<AzureBlobStorageOptions> _blobOptions;
    private readonly IOptionsSnapshot<BaGetterOptions> _root;
    private readonly ILogger<AzureFeedMigrationService> _logger;

    public AzureFeedMigrationService(
        TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient,
        IOptionsSnapshot<AzureTableOptions> tableOptions,
        IOptionsSnapshot<AzureBlobStorageOptions> blobOptions,
        IOptionsSnapshot<BaGetterOptions> root,
        ILogger<AzureFeedMigrationService> logger)
    {
        _tableServiceClient = tableServiceClient ?? throw new ArgumentNullException(nameof(tableServiceClient));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _tableOptions = tableOptions ?? throw new ArgumentNullException(nameof(tableOptions));
        _blobOptions = blobOptions ?? throw new ArgumentNullException(nameof(blobOptions));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FeedMigrationResult> MigrateLegacyRootToFeedAsync(string targetFeed, bool dryRun, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetFeed))
        {
            throw new ArgumentException("Target feed is required", nameof(targetFeed));
        }

        if (!FeedUtility.TryFindFeed(_root.Value, targetFeed, out var configuredFeed))
        {
            throw new InvalidOperationException($"Target feed '{targetFeed}' was not found in Feeds configuration.");
        }

        var partitionPrefix = string.IsNullOrWhiteSpace(configuredFeed.Database?.PartitionPrefix)
            ? configuredFeed.Name
            : configuredFeed.Database.PartitionPrefix;
        var blobPrefix = string.IsNullOrWhiteSpace(configuredFeed.Storage?.Prefix)
            ? configuredFeed.Name
            : configuredFeed.Storage.Prefix;

        var table = _tableServiceClient.GetTableClient(_tableOptions.Value.TableName);
        var containerName = string.IsNullOrWhiteSpace(configuredFeed.Storage?.Container)
            ? _blobOptions.Value.Container
            : configuredFeed.Storage.Container;
        var container = _blobServiceClient.GetBlobContainerClient(containerName);

        var tableCopied = 0;
        var tableSkipped = 0;
        await foreach (var entity in table.QueryAsync<PackageEntity>(cancellationToken: cancellationToken))
        {
            // Legacy entities use package-id-only partition keys (no feed delimiter).
            if (entity.PartitionKey?.Contains('|') == true)
            {
                continue;
            }

            var id = entity.Id ?? entity.PartitionKey;
            var targetPk = TableOperationBuilder.GetPartitionKey(partitionPrefix, id);
            var targetRk = entity.RowKey;

            var existing = await table.GetEntityIfExistsAsync<PackageEntity>(targetPk, targetRk, cancellationToken: cancellationToken);
            if (existing.HasValue)
            {
                tableSkipped++;
                continue;
            }

            tableCopied++;
            if (!dryRun)
            {
                entity.PartitionKey = targetPk;
                entity.RowKey = targetRk;
                entity.FeedName = configuredFeed.Name;
                await table.AddEntityAsync(entity, cancellationToken);
            }
        }

        var blobCopied = 0;
        var blobSkipped = 0;
        await foreach (var blob in container.GetBlobsAsync(prefix: "packages/", cancellationToken: cancellationToken))
        {
            var sourcePath = blob.Name;
            var suffix = sourcePath.Substring("packages/".Length);
            var destinationPath = $"{blobPrefix.Trim('/')}/{suffix}";

            var sourceClient = container.GetBlobClient(sourcePath);
            var destinationClient = container.GetBlobClient(destinationPath);
            if (await destinationClient.ExistsAsync(cancellationToken))
            {
                blobSkipped++;
                continue;
            }

            blobCopied++;
            if (!dryRun)
            {
                await destinationClient.StartCopyFromUriAsync(sourceClient.Uri, cancellationToken: cancellationToken);
            }
        }

        _logger.LogInformation(
            "Feed migration complete. DryRun={DryRun}, TableCopied={TableCopied}, TableSkipped={TableSkipped}, BlobCopied={BlobCopied}, BlobSkipped={BlobSkipped}",
            dryRun, tableCopied, tableSkipped, blobCopied, blobSkipped);

        return new FeedMigrationResult(tableCopied, tableSkipped, blobCopied, blobSkipped);
    }
}
