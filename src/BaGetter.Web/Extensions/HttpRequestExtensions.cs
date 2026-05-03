using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.AspNetCore.Http;

namespace BaGetter.Web;

public static class HttpRequestExtensions
{
    public const string ApiKeyHeader = "X-NuGet-ApiKey";

    public static async Task<Stream> GetUploadStreamOrNullAsync(this HttpRequest request, CancellationToken cancellationToken)
    {
        // Try to get the nupkg from the multipart/form-data. If that's empty,
        // fallback to the request's body.
        Stream rawUploadStream = null;
        try
        {
            if (request.HasFormContentType && request.Form.Files.Count > 0)
            {
                rawUploadStream = request.Form.Files[0].OpenReadStream();
            }
            else
            {
                // Enable buffering so the body can be read even if form parsing consumed it.
                request.EnableBuffering();
                request.Body.Position = 0;
                rawUploadStream = request.Body;
            }

            // Convert the upload stream into a temporary file stream to
            // minimize memory usage.
            return await rawUploadStream?.AsTemporaryFileStreamAsync(cancellationToken);
        }
        finally
        {
            rawUploadStream?.Dispose();
        }
    }

    public static string GetApiKey(this HttpRequest request)
    {
        return request.Headers[ApiKeyHeader];
    }
}
