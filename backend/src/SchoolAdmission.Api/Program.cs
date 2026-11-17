using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Endpoints;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Errors: RFC 9457 problem details for everything, bad input is always a 4xx.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter()));

// Configuration from environment variables (HOLD_DURATION_SECONDS etc.).
builder.Services.AddOptions<AdmissionOptions>()
    .Configure<IConfiguration>(AdmissionOptions.Bind)
    .ValidateOnStart();

// Database: connection string from ConnectionStrings__Default.
builder.Services.AddDbContext<AdmissionDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Database connection string is missing. Set the ConnectionStrings__Default environment variable.");
    }

    options.UseNpgsql(connectionString);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddHostedService<HoldExpirySweeper>();

// CORS: the React app is served from a different origin. No cookies are used, so any listed
// origin (or any origin when CORS_ALLOWED_ORIGINS is unset or "*") may call the API.
var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "*")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(allowedOrigins);
    }

    policy.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapGet("/health", async (AdmissionDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ok" })
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unreachable"))
    .ExcludeFromDescription();

app.MapClassEndpoints();
app.MapHoldEndpoints();

await app.InitializeDatabaseAsync();
await app.RunAsync();

/// <summary>Entry point; public so the integration tests can host the app.</summary>
public partial class Program;
