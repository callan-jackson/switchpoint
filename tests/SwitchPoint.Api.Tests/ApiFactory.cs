using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Infrastructure.Persistence;
using SwitchPoint.Infrastructure.Seeding;

namespace SwitchPoint.Api.Tests;

/// <summary>Boots the API against a throwaway SQLite database with the demo firm seeded.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"switchpoint-api-{Guid.NewGuid():N}.db");
    private readonly string _reportsPath = Path.Combine(Path.GetTempPath(), $"switchpoint-api-reports-{Guid.NewGuid():N}");
    private readonly Dictionary<string, string?> _extraSettings;
    private readonly string? _webRootPath;

    public ApiFactory(Dictionary<string, string?>? extraSettings = null, string? webRootPath = null)
    {
        _extraSettings = extraSettings ?? [];
        _webRootPath = webRootPath;
    }

    public static JsonSerializerOptions Json { get; } = CreateJsonOptions();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
        builder.UseContentRoot(ApiContentRoot());
        if (_webRootPath is not null)
        {
            builder.UseWebRoot(_webRootPath);
        }
        builder.ConfigureAppConfiguration((_, config) =>
        {
            Dictionary<string, string?> settings = new()
            {
                ["ConnectionStrings:SwitchPoint"] = $"Data Source={_databasePath}",
                ["Database:Provider"] = "Sqlite",
                ["Database:MigrateOnStartup"] = "true",
                ["Seed:Demo"] = "true",
                ["Reports:Path"] = _reportsPath,
                ["Auth:SigningKey"] = "integration-test-signing-key-integration-test-signing-key",
                ["Integrations:Intelliflo:Mode"] = "Sandbox",
                ["Integrations:Morningstar:Mode"] = "Sandbox",
            };
            foreach ((string key, string? value) in _extraSettings)
            {
                settings[key] = value;
            }

            config.AddInMemoryCollection(settings);
        });
    }

    /// <summary>The API project directory, so the seeder finds the repository's data folder.</summary>
    public static string ApiContentRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SwitchPoint.sln")))
        {
            dir = dir.Parent;
        }

        return dir is null ? AppContext.BaseDirectory : Path.Combine(dir.FullName, "src", "SwitchPoint.Api");
    }

    /// <summary>Logs in and returns a client whose requests carry the bearer token.</summary>
    public async Task<HttpClient> AuthenticatedClientAsync(string email = "adviser@demo.switchpoint.local")
    {
        HttpClient client = CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = DataSeeder.DemoPassword }, Json);
        response.EnsureSuccessStatusCode();
        LoginResponse login = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    /// <summary>Runs an action against the database (used to plant another firm's data).</summary>
    public async Task WithDbAsync(Func<SwitchPointDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using IServiceScope scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            File.Delete(_databasePath);
            if (Directory.Exists(_reportsPath))
            {
                Directory.Delete(_reportsPath, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

/// <summary>One factory shared by the read-only tests in a class.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new();

    public HttpClient Adviser { get; private set; } = null!;

    public async Task InitializeAsync() => Adviser = await Factory.AuthenticatedClientAsync();

    public Task DisposeAsync()
    {
        Adviser.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }
}
