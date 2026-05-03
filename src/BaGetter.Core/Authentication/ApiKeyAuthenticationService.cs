using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BaGetter.Core;

public class ApiKeyAuthenticationService : IAuthenticationService
{
    private readonly string _apiKey;
    private readonly ApiKey[] _apiKeys;
    private readonly BaGetterOptions _options;

    public ApiKeyAuthenticationService(IOptionsSnapshot<BaGetterOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _apiKey = string.IsNullOrEmpty(_options.ApiKey) ? null : _options.ApiKey;
        _apiKeys = _options.Authentication?.ApiKeys ?? [];
    }

    public Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
        => Task.FromResult(Authenticate(apiKey));

    public Task<bool> AuthenticateAsync(string feedName, string apiKey, CancellationToken cancellationToken)
        => Task.FromResult(Authenticate(feedName, apiKey));

    private bool Authenticate(string apiKey)
    {
        // No authentication is necessary if there is no required API key.
        if (_apiKey == null && (_apiKeys.Length == 0)) return true;

        return _apiKey == apiKey || _apiKeys.Any(x => x.Key.Equals(apiKey));
    }

    private bool Authenticate(string feedName, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(feedName))
        {
            return Authenticate(apiKey);
        }

        if (!FeedUtility.IsMultiFeedConfigured(_options))
        {
            return Authenticate(apiKey);
        }

        if (!FeedUtility.TryFindFeed(_options, feedName, out var feed))
        {
            return false;
        }

        if (feed.ApiKeys == null || feed.ApiKeys.Count == 0)
        {
            // Compatibility: default feed can fallback to the global key model.
            var defaultFeed = FeedUtility.GetDefaultFeed(_options)?.Name ?? _options.FeedRouting?.DefaultFeed;
            if (defaultFeed != null && defaultFeed.Equals(feedName, StringComparison.OrdinalIgnoreCase))
            {
                return Authenticate(apiKey);
            }

            return false;
        }

        foreach (var feedKey in feed.ApiKeys)
        {
            if (!string.IsNullOrEmpty(feedKey.Key) && feedKey.Key == apiKey)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(feedKey.KeySha256) && !string.IsNullOrEmpty(apiKey))
            {
                var hash = ComputeSha256(apiKey);
                if (feedKey.KeySha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
