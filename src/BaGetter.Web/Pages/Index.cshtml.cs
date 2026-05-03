using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Core.Configuration;
using BaGetter.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BaGetter.Web;

public class IndexModel : PageModel
{
    private readonly ISearchService _search;
    private readonly IOptionsSnapshot<BaGetterOptions> _options;

    public IndexModel(ISearchService search, IOptionsSnapshot<BaGetterOptions> options)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public const int ResultsPerPage = 20;

    [BindProperty(Name = "q", SupportsGet = true)]
    public string Query { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string PackageType { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public string Framework { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public bool Prerelease { get; set; } = true;

    public IReadOnlyList<SearchResult> Packages { get; private set; }

    public bool ShowFeedsView { get; private set; }
    public IReadOnlyList<FeedOptions> Feeds { get; private set; } = [];
    public string DefaultFeed { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        var options = _options.Value;
        var feedRoute = RouteData.Values["feed"]?.ToString();

        // If multi-feed is configured and no feed is selected, show the feeds overview
        if (FeedUtility.IsMultiFeedConfigured(options) && string.IsNullOrEmpty(feedRoute))
        {
            ShowFeedsView = true;
            Feeds = options.Feeds ?? [];
            DefaultFeed = options.FeedRouting?.DefaultFeed ?? "internal";
            return Page();
        }

        var packageType = PackageType == "any" ? null : PackageType;
        var framework = Framework == "any" ? null : Framework;

        var search = await _search.SearchAsync(
            new SearchRequest
            {
                Skip = (PageIndex - 1) * ResultsPerPage,
                Take = ResultsPerPage,
                IncludePrerelease = Prerelease,
                IncludeSemVer2 = true,
                PackageType = packageType,
                Framework = framework,
                Query = Query,
            },
            cancellationToken);

        Packages = search.Data;

        return Page();
    }
}
