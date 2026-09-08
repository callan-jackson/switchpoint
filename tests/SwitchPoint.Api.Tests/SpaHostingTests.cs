using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Net.Http.Headers;

namespace SwitchPoint.Api.Tests;

/// <summary>
/// The API hosts the built front end. A fallback authorization policy applies to requests that match no
/// endpoint, so static files served after the authorization middleware come back 401 and the browser shows a
/// blank page — the bundles must be served before it. These tests stand up a throwaway wwwroot to prove it.
/// </summary>
[SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit disposes the fixture through IAsyncLifetime.DisposeAsync, which disposes the factory.")]
public sealed class SpaHostingTests : IAsyncLifetime
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"switchpoint-wwwroot-{Guid.NewGuid():N}");
    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>SwitchPoint</title><div id=\"root\"></div>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-test.js"), "export const ok = true;\n");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-test.css"), ":root{--x:1}\n");
        _factory = new ApiFactory(webRootPath: _webRoot);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        try
        {
            Directory.Delete(_webRoot, true);
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/assets/index-test.js", "text/javascript")]
    [InlineData("/assets/index-test.css", "text/css")]
    public async Task Spa_bundles_are_served_without_a_token(string path, string expectedContentType)
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedContentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_index_is_served_at_the_root_and_for_client_side_routes()
    {
        using HttpClient client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (string path in new[] { "/", "/clients", "/pension-switch/some-id" })
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("id=\"root\"", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Hosting_the_spa_does_not_open_up_the_api()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/clients", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Static_files_still_carry_the_security_headers()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/assets/index-test.js", UriKind.Relative));

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains(HeaderNames.ContentSecurityPolicy));
    }
}
