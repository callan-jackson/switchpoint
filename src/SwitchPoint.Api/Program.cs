var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

/// <summary>Entry point marker used by integration tests (WebApplicationFactory).</summary>
public partial class Program;
