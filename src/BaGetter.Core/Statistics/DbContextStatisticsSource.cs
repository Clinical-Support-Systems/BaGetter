using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BaGetter.Core.Statistics;

public class DbContextStatisticsSource : IStatisticsSource
{
    private readonly IContext _context;

    public DbContextStatisticsSource(IContext context)
    {
        _context = context;
    }

    public async Task<StatisticsTotals> GetTotalsAsync()
    {
        var packagesTotalAmount = await _context.Packages
            .Select(p => p.Id)
            .Distinct()
            .CountAsync();
        var versionsTotalAmount = await _context.Packages.CountAsync();

        return new StatisticsTotals(packagesTotalAmount, versionsTotalAmount);
    }
}
