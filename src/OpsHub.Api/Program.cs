using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Data;
using OpsHub.Api.Data.Seeding;
using OpsHub.Domain.Users;
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

// ---------- Identity (user store + password hashing) ----------
// Sign-in, cookies and authorisation policies are added in Phase 3.
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Follows current NIST guidance (SP 800-63B): require length, not character-class rules.
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<OpsHubDbContext>();

builder.Services.AddScoped<DevSeeder>();

// ---------- Web API ----------
builder.Services.AddControllers();
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
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar: interactive API documentation
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

await app.RunAsync();
