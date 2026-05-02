using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BaGetter.Azure.Aspire.Tests;

public class AzureStoragePushIntegrationTests
{
    private const string ApiKeyHeader = "X-NuGet-ApiKey";
    private const string PackageFileName = "TestData.1.2.3.nupkg";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task PackagePushSucceedsAndDuplicatePushReturnsConflict()
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var cancellationToken = cts.Token;

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BaGetter_Azure_Aspire_AppHost>(cancellationToken);

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        appHost.Services.AddLogging(logging => logging
            .AddConsole()
            .AddFilter("Default", LogLevel.Information)
            .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
            .AddFilter("Aspire.Hosting.Dcp", LogLevel.Warning));

        await using var app = await appHost.BuildAsync(cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        await app.ResourceNotifications.WaitForResourceHealthyAsync("bagetter", cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        using var client = app.CreateHttpClient("bagetter");

        using var firstResponse = await PushPackageAsync(client, cancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            firstResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, got {(int)firstResponse.StatusCode} {firstResponse.StatusCode}. Body:{Environment.NewLine}{firstContent}");

        using var downloadResponse = await client.GetAsync(
            "v3/package/TestData/1.2.3/TestData.1.2.3.nupkg",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        using var secondResponse = await PushPackageAsync(client, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> PushPackageAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var packagePath = Path.Combine(AppContext.BaseDirectory, "TestData", PackageFileName);
        var packageStream = File.OpenRead(packagePath);
        var content = new StreamContent(packageStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var request = new HttpRequestMessage(HttpMethod.Put, "api/v2/package")
        {
            Content = content,
        };
        request.Headers.Add(ApiKeyHeader, "aspire-smoke-test");

        var response = await client.SendAsync(request, cancellationToken);

        content.Dispose();
        packageStream.Dispose();
        request.Dispose();

        return response;
    }
}
