using System;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGetter.Web;

public class PackagePublishController : Controller
{
    private readonly IAuthenticationService _authentication;
    private readonly IPackageIndexingService _indexer;
    private readonly IPackageDatabase _packages;
    private readonly IPackageDeletionService _deleteService;
    private readonly IOptionsSnapshot<BaGetterOptions> _options;
    private readonly IFeedContextAccessor _feed;
    private readonly ILogger<PackagePublishController> _logger;

    public PackagePublishController(
        IAuthenticationService authentication,
        IPackageIndexingService indexer,
        IPackageDatabase packages,
        IPackageDeletionService deletionService,
        IOptionsSnapshot<BaGetterOptions> options,
        IFeedContextAccessor feed,
        ILogger<PackagePublishController> logger)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _deleteService = deletionService ?? throw new ArgumentNullException(nameof(deletionService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // See: https://docs.microsoft.com/en-us/nuget/api/package-publish-resource#push-a-package
    public async Task Upload(CancellationToken cancellationToken)
    {
        if (IsReadOnly() ||
            !await _authentication.AuthenticateAsync(_feed.Current?.Name, Request.GetApiKey(), cancellationToken))
        {
            HttpContext.Response.StatusCode = 401;
            return;
        }

        if (IsLicensedFeedBlocked())
        {
            HttpContext.Response.StatusCode = 403;
            return;
        }

        try
        {
            using var uploadStream = await Request.GetUploadStreamOrNullAsync(cancellationToken);
            if (uploadStream == null)
            {
                _logger.LogWarning(
                    "Package upload rejected: upload stream is null. ContentType={ContentType}, ContentLength={ContentLength}",
                    Request.ContentType,
                    Request.ContentLength);
                HttpContext.Response.StatusCode = 400;
                return;
            }

            _logger.LogInformation(
                "Package upload stream received, length={Length}. Indexing...",
                uploadStream.Length);

            var result = await _indexer.IndexAsync(uploadStream, cancellationToken: cancellationToken);

            switch (result)
            {
                case PackageIndexingResult.InvalidPackage:
                    _logger.LogWarning("Package upload rejected: package is invalid");
                    HttpContext.Response.StatusCode = 400;
                    break;

                case PackageIndexingResult.PackageAlreadyExists:
                    HttpContext.Response.StatusCode = 409;
                    break;

                case PackageIndexingResult.Success:
                    HttpContext.Response.StatusCode = 201;
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown during package upload");

            HttpContext.Response.StatusCode = 500;
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(string id, string version, CancellationToken cancellationToken)
    {
        if (IsReadOnly())
        {
            return Unauthorized();
        }

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return NotFound();
        }

        if (!await _authentication.AuthenticateAsync(_feed.Current?.Name, Request.GetApiKey(), cancellationToken))
        {
            return Unauthorized();
        }

        if (await _deleteService.TryDeletePackageAsync(id, nugetVersion, cancellationToken))
        {
            return NoContent();
        }
        else
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Relist(string id, string version, CancellationToken cancellationToken)
    {
        if (IsReadOnly())
        {
            return Unauthorized();
        }

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return NotFound();
        }

        if (!await _authentication.AuthenticateAsync(_feed.Current?.Name, Request.GetApiKey(), cancellationToken))
        {
            return Unauthorized();
        }

        if (await _packages.RelistPackageAsync(id, nugetVersion, cancellationToken))
        {
            return Ok();
        }
        else
        {
            return NotFound();
        }
    }

    private bool IsReadOnly()
    {
        if (_options.Value.IsReadOnlyMode)
        {
            return true;
        }

        if (!FeedUtility.IsMultiFeedConfigured(_options.Value))
        {
            return false;
        }

        if (!FeedUtility.TryFindFeed(_options.Value, _feed.Current?.Name, out var feed))
        {
            return false;
        }

        return feed.IsReadOnly;
    }

    private bool IsLicensedFeedBlocked()
    {
        var options = _options.Value;
        if (!FeedUtility.IsMultiFeedConfigured(options))
        {
            return false;
        }

        if (!FeedUtility.TryFindFeed(options, _feed.Current?.Name, out var feed))
        {
            return false;
        }

        if (!feed.Name.Equals("licensed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Guardrail requested by CSS: do not publish licensed/customer packages
        // until feed-level read authentication exists and is enabled.
        return !feed.RequireReadAuthentication;
    }
}
