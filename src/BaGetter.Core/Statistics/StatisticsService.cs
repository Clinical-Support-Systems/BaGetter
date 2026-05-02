using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BaGetter.Core.Statistics;

public class StatisticsService : IStatisticsService
{
    private readonly IConfiguration _configuration;
    private readonly IStatisticsSource _statisticsSource;
    private Task<StatisticsTotals> _totalsTask;

    public StatisticsService(
        IConfiguration configuration,
        IStatisticsSource statisticsSource)
    {
        _configuration = configuration;
        _statisticsSource = statisticsSource;
    }

    public async Task<int> GetPackagesTotalAmount()
    {
        return (await GetTotalsAsync()).PackagesTotalAmount;
    }

    public async Task<int> GetVersionsTotalAmount()
    {
        return (await GetTotalsAsync()).VersionsTotalAmount;
    }

    public IEnumerable<string> GetKnownServices()
    {
        var servicesNames = new List<string>();

        // Database providers.
        if (_configuration.HasDatabaseType("AzureTable")) servicesNames.Add("AzureTable");
        if (_configuration.HasDatabaseType("MySql")) servicesNames.Add("MySql");
        if (_configuration.HasDatabaseType("PostgreSql")) servicesNames.Add("PostgreSql");
        if (_configuration.HasDatabaseType("SqlServer")) servicesNames.Add("SqlServer");
        if (_configuration.HasDatabaseType("Sqlite")) servicesNames.Add("Sqlite");

        // Storage providers.
        if (_configuration.HasStorageType("FileSystem")) servicesNames.Add("FileSystem");
        if (_configuration.HasStorageType("AwsS3")) servicesNames.Add("AwsS3");
        if (_configuration.HasStorageType("AliyunOss")) servicesNames.Add("AliyunOss");
        if (_configuration.HasStorageType("AzureBlobStorage")) servicesNames.Add("AzureBlobStorage");
        if (_configuration.HasStorageType("GoogleCloud")) servicesNames.Add("GoogleCloud");
        if (_configuration.HasStorageType("TencentCos")) servicesNames.Add("TencentCos");
        return servicesNames;
    }

    private Task<StatisticsTotals> GetTotalsAsync()
    {
        return _totalsTask ??= _statisticsSource.GetTotalsAsync();
    }
}
