using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using BaGetter.Core;
using BaGetter.Core.Configuration;
using BaGetter.Protocol.Models;
using Microsoft.Extensions.Options;

namespace BaGetter.Azure
{
    public class TableSearchService : ISearchService
    {
        private readonly TableClient _table;
        private readonly ISearchResponseBuilder _responseBuilder;
        private readonly IFeedContextAccessor _feed;
        private readonly IOptionsSnapshot<BaGetterOptions> _root;

        public TableSearchService(
            TableServiceClient client,
            ISearchResponseBuilder responseBuilder,
            IOptionsSnapshot<AzureTableOptions> options,
            IFeedContextAccessor feed,
            IOptionsSnapshot<BaGetterOptions> root)
        {
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(responseBuilder, nameof(responseBuilder));
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            _table = client.GetTableClient(options.Value.TableName);
            _responseBuilder = responseBuilder;
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var results = await SearchAsync(
                request.Query,
                request.Skip,
                request.Take,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                cancellationToken);

            return _responseBuilder.BuildSearch(results);
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(
            AutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            var results = await SearchAsync(
                request.Query,
                request.Skip,
                request.Take,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                cancellationToken);

            var packageIds = results.Select(p => p.PackageId).ToList();

            return _responseBuilder.BuildAutocomplete(packageIds);
        }

        public Task<AutocompleteResponse> ListPackageVersionsAsync(
            VersionsRequest request,
            CancellationToken cancellationToken)
        {
            return ListVersionsAsync(request, cancellationToken);
        }

        public Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
        {
            var response = _responseBuilder.BuildDependents(new List<PackageDependent>());

            return Task.FromResult(response);
        }

        private async Task<List<PackageRegistration>> SearchAsync(
            string searchText,
            int skip,
            int take,
            bool includePrerelease,
            bool includeSemVer2,
            CancellationToken cancellationToken)
        {
            var query = _table.QueryAsync<PackageEntity>(GenerateSearchFilter(searchText, includePrerelease, includeSemVer2), cancellationToken: cancellationToken);

            var results = await LoadPackagesAsync(query, maxPartitions: skip + take);

            return results
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PackageRegistration(group.Key, group.ToList()))
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        private async Task<AutocompleteResponse> ListVersionsAsync(VersionsRequest request, CancellationToken cancellationToken)
        {
            var partitionKey = GetPartitionKey(request.PackageId);
            var partitionFilter = $"PartitionKey eq '{partitionKey}'";
            var listedFilter = "Listed eq true";
            var preFilter = request.IncludePrerelease ? string.Empty : "IsPrerelease eq false";
            var semverFilter = request.IncludeSemVer2 ? string.Empty : "SemVerLevel eq 0";

            var filter = GenerateAnd(partitionFilter, listedFilter);
            filter = GenerateAnd(filter, preFilter);
            filter = GenerateAnd(filter, semverFilter);

            var query = _table.QueryAsync<PackageEntity>(filter, cancellationToken: cancellationToken);
            var versions = new List<string>();
            await foreach (var entity in query)
            {
                versions.Add(entity.NormalizedVersion.ToLowerInvariant());
            }

            versions.Sort(StringComparer.OrdinalIgnoreCase);
            return _responseBuilder.BuildAutocomplete(versions);
        }

        private static async Task<IReadOnlyList<Package>> LoadPackagesAsync(
            AsyncPageable<PackageEntity> query,
            int maxPartitions)
        {
            var results = new List<Package>();

            var partitions = 0;
            string lastPartitionKey = null;

            await foreach (var result in query)
            {
                if (lastPartitionKey != result.PartitionKey)
                {
                    lastPartitionKey = result.PartitionKey;
                    partitions++;

                    if (partitions > maxPartitions)
                    {
                        break;
                    }
                }

                results.Add(result.AsPackage());
            }

            return results;
        }

        private string GenerateSearchFilter(string searchText, bool includePrerelease, bool includeSemVer2)
        {
            var result = "";
            var feedPartitionPrefix = GetFeedPartitionPrefix(_feed.Current?.Name, _root.Value);
            var isLegacyMode = string.IsNullOrWhiteSpace(feedPartitionPrefix);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var prefix = searchText.TrimEnd().Split(separator: null).Last();

                var prefixLower = isLegacyMode
                    ? prefix.ToLowerInvariant()
                    : $"{feedPartitionPrefix}|{prefix.ToLowerInvariant()}";
                var prefixUpper = isLegacyMode
                    ? $"{prefix.ToLowerInvariant()}~"
                    : $"{feedPartitionPrefix}|{prefix.ToLowerInvariant()}~";

                var partitionLowerFilter = $"PartitionKey ge '{prefixLower}'";
                var partitionUpperFilter = $"PartitionKey lt '{prefixUpper}'";

                result = GenerateAnd(partitionLowerFilter, partitionUpperFilter);
            }
            else if (!isLegacyMode)
            {
                var feedLower = $"{feedPartitionPrefix}|";
                var feedUpper = $"{feedPartitionPrefix}|~";
                result = GenerateAnd($"PartitionKey ge '{feedLower}'", $"PartitionKey lt '{feedUpper}'");
            }

            result = GenerateAnd(result, "Listed eq true");

            if (!includePrerelease)
            {
                result = GenerateAnd(result, "IsPrerelease eq false");
            }

            if (!includeSemVer2)
            {
                result = GenerateAnd(result, "SemVerLevel eq 0");
            }

            return result;
        }

        private static string GenerateAnd(string left, string right)
        {
            if (string.IsNullOrEmpty(left)) return right;
            if (string.IsNullOrEmpty(right)) return left;

            return $"({left}) and ({right})";
        }

        private string GetPartitionKey(string id)
        {
            var feedPrefix = GetFeedPartitionPrefix(_feed.Current?.Name, _root.Value);
            if (string.IsNullOrWhiteSpace(feedPrefix))
            {
                return TableOperationBuilder.GetPartitionKey(id);
            }

            return TableOperationBuilder.GetPartitionKey(feedPrefix, id);
        }

        private static string GetFeedPartitionPrefix(string feedName, BaGetterOptions options)
        {
            if (!FeedUtility.IsMultiFeedConfigured(options) || string.IsNullOrWhiteSpace(feedName))
            {
                return null;
            }

            if (FeedUtility.TryFindFeed(options, feedName, out var feed)
                && !string.IsNullOrWhiteSpace(feed.Database?.PartitionPrefix))
            {
                return feed.Database.PartitionPrefix.ToLowerInvariant();
            }

            return feedName.ToLowerInvariant();
        }

    }
}
