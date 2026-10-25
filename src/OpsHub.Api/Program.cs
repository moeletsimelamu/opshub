using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Data;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Dashboard;
using OpsHub.Api.Features.Tickets;
using OpsHub.Api.Features.Users;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
// ILogger message templates ("Seeded {UserCount} users") are already structured.
// Outside Development, write one JSON object per log line so a log platform can index the fields.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

// ---------- Configuration ----------
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

// Injected instead of DateTime.UtcNow so tests can control the clock.
builder.Services.AddSingleton(TimeProvider.System);

// ---------- Database ----------
// The connection string is read when the DbContext is created, so tests and
// environment variables (ConnectionStrings__OpsHub) can override it.
builder.Services.AddDbContext<OpsHubDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("OpsHub")
        ?? throw new InvalidOperationException("Connection string 'OpsHub' is not configured.");
    options.UseSqlite(connectionString);
});

// ---------- Identity, session cookie, authorisation, CSRF, rate limiting ----------
builder.Services.AddOpsHubAuth(builder.Configuration);

// ---------- Application services ----------
builder.Services.AddScoped<DevSeeder>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<TicketActivityService>();
builder.Services.AddScoped<DashboardService>();

// ---------- Web API ----------
builder.Services.AddControllers(options =>
{
    // Every POST/PUT/PATCH/DELETE must carry a valid X-XSRF-TOKEN header.
    options.Filters.Add<XsrfValidationFilter>();

    // Validation errors use the JSON property names the client sent ("email", not "Email").
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
})
.AddJsonOptions(options =>
{
    // Enums travel as readable names ("InProgress") instead of numbers, which makes the
    // API contract self-explanatory and safe if enum values are ever reordered.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();

// Every error response becomes an RFC 9457 ProblemDetails JSON body with a traceId.
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks().AddDbContextCheck<OpsHubDbContext>("database");

var app = builder.Build();

await app.InitialiseDatabaseAsync();

// ---------- HTTP pipeline (order matters) ----------
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();                 // /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous();      // /scalar: interactive API documentation
}

app.UseHttpsRedirection();

app.UseAuthentication();   // Who is this? (reads the session cookie)
app.UseAuthorization();    // Are they allowed? (policies, [Authorize], fallback policy)
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

await app.RunAsync();
