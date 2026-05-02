using System.Threading.Tasks;

namespace BaGetter.Core.Statistics;

public interface IStatisticsSource
{
    Task<StatisticsTotals> GetTotalsAsync();
}

public readonly record struct StatisticsTotals(int PackagesTotalAmount, int VersionsTotalAmount);
