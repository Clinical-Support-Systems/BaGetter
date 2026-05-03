using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using BaGetter.Azure;
using BaGetter.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging;
using NuGet.Versioning;
using Xunit;

namespace BaGetter.Core.Tests.Azure;

public class TablePackageDatabaseTests
{
    private const string TableName = "packages";

    [Fact]
    public async Task ExistsAsyncWithIdUsesNormalizedPartitionKeyFilter()
    {
        var id = "BaGetter.Test";
        var expectedFilter = TableClient.CreateQueryFilter<PackageEntity>(
            p => p.PartitionKey == TableOperationBuilder.GetPartitionKey(id));

        var table = new Mock<TableClient>(MockBehavior.Strict);
        table.Setup(t => t.QueryAsync<PackageEntity>(
                It.Is<string>(filter => filter == expectedFilter),
                1,
                It.Is<IEnumerable<string>>(columns => HasSingleColumn(columns, "PartitionKey")),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(new PackageEntity { PartitionKey = "bagetter.test" }));

        var target = CreateTarget(table);

        var exists = await target.ExistsAsync(id, CancellationToken.None);

        Assert.True(exists);
        table.VerifyAll();
    }

    [Fact]
    public async Task ExistsAsyncWithIdAndVersionUsesNormalizedKeys()
    {
        var id = "BaGetter.Test";
        var version = NuGetVersion.Parse("1.0.0");

        var table = new Mock<TableClient>(MockBehavior.Strict);
        table.Setup(t => t.GetEntityAsync<PackageEntity>(
                TableOperationBuilder.GetPartitionKey(id),
                TableOperationBuilder.GetRowKey(version),
                It.Is<IEnumerable<string>>(columns => HasSingleColumn(columns, "PartitionKey")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new PackageEntity(), Mock.Of<Response>()));

        var target = CreateTarget(table);

        var exists = await target.ExistsAsync(id, version, CancellationToken.None);

        Assert.True(exists);
        table.VerifyAll();
    }

    [Fact]
    public async Task ExistsAsyncWithIdAndVersionReturnsFalseOnNotFound()
    {
        var id = "BaGetter.Test";
        var version = NuGetVersion.Parse("1.0.0");

        var table = new Mock<TableClient>(MockBehavior.Strict);
        table.Setup(t => t.GetEntityAsync<PackageEntity>(
                TableOperationBuilder.GetPartitionKey(id),
                TableOperationBuilder.GetRowKey(version),
                It.Is<IEnumerable<string>>(columns => HasSingleColumn(columns, "PartitionKey")),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var target = CreateTarget(table);

        var exists = await target.ExistsAsync(id, version, CancellationToken.None);

        Assert.False(exists);
        table.VerifyAll();
    }

    [Fact]
    public async Task IndexAsyncReturnsPackageAlreadyExistsWhenAzureTableRowExists()
    {
        var builder = new PackageBuilder
        {
            Id = "BaGetter.Test",
            Version = NuGetVersion.Parse("1.0.0"),
            Description = "Test Description",
        };
        builder.Authors.Add("Test Author");
        builder.Files.Add(new PhysicalPackageFile
        {
            SourcePath = GetType().Assembly.Location,
            TargetPath = "lib/Test.dll"
        });

        using var stream = new MemoryStream();
        builder.Save(stream);

        var table = new Mock<TableClient>(MockBehavior.Strict);
        table.Setup(t => t.GetEntityAsync<PackageEntity>(
                TableOperationBuilder.GetPartitionKey(builder.Id),
                TableOperationBuilder.GetRowKey(builder.Version),
                It.Is<IEnumerable<string>>(columns => HasSingleColumn(columns, "PartitionKey")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new PackageEntity(), Mock.Of<Response>()));

        var database = CreateTarget(table);
        var storage = new Mock<IPackageStorageService>(MockBehavior.Strict);
        var deleter = new Mock<IPackageDeletionService>(MockBehavior.Strict);
        var search = new Mock<ISearchIndexer>(MockBehavior.Strict);
        var time = new Mock<SystemTime>(MockBehavior.Loose);
        var options = new Mock<IOptionsSnapshot<BaGetterOptions>>(MockBehavior.Strict);
        options.Setup(o => o.Value).Returns(new BaGetterOptions
        {
            AllowPackageOverwrites = PackageOverwriteAllowed.False,
        });

        var retentionOptions = new Mock<IOptionsSnapshot<RetentionOptions>>(MockBehavior.Strict);
        retentionOptions.Setup(o => o.Value).Returns(new RetentionOptions());

        var target = new PackageIndexingService(
            database,
            storage.Object,
            deleter.Object,
            search.Object,
            time.Object,
            options.Object,
            retentionOptions.Object,
            NullLogger<PackageIndexingService>.Instance);

        var result = await target.IndexAsync(stream, CancellationToken.None);

        Assert.Equal(PackageIndexingResult.PackageAlreadyExists, result);
        table.VerifyAll();
    }

    private static TablePackageDatabase CreateTarget(Mock<TableClient> table)
    {
        var serviceClient = new Mock<TableServiceClient>(MockBehavior.Strict);
        serviceClient.Setup(c => c.GetTableClient(TableName)).Returns(table.Object);
        var root = new Mock<IOptionsSnapshot<BaGetterOptions>>(MockBehavior.Strict);
        root.SetupGet(r => r.Value).Returns(new BaGetterOptions());

        return new TablePackageDatabase(
            serviceClient.Object,
            NullLogger<TablePackageDatabase>.Instance,
            Options.Create(new AzureTableOptions { TableName = TableName }),
            new FeedContextAccessor { Current = new FeedContext { IsLegacySingleFeed = true } },
            root.Object);
    }

    private static AsyncPageable<PackageEntity> CreateAsyncPageable(params PackageEntity[] entities)
    {
        return AsyncPageable<PackageEntity>.FromPages(
        [
            Page<PackageEntity>.FromValues(entities, continuationToken: null, Mock.Of<Response>())
        ]);
    }

    private static bool HasSingleColumn(IEnumerable<string> columns, string expected)
    {
        return columns is not null && new List<string>(columns).SequenceEqual([expected]);
    }
}
