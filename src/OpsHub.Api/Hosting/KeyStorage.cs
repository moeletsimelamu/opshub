using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

namespace OpsHub.Api.Hosting;

/// <summary>Bound from the "DataProtection" configuration section.</summary>
public sealed class KeyStorageOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Where the keys that sign session cookies and XSRF tokens are kept. In a container this
    /// must be on persistent storage: if the keys are lost on restart, every user is signed
    /// out and every open form's XSRF token stops working.
    /// </summary>
    public string? KeysDirectory { get; set; }
}

internal static partial class KeyStorage
{
    public static IServiceCollection AddOpsHubKeyStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KeyStorageOptions>(configuration.GetSection(KeyStorageOptions.SectionName));

        // A fixed name, so keys stay valid when the app is installed in a different folder.
        services.AddDataProtection().SetApplicationName("OpsHub");

        // What PersistKeysToFileSystem does, but read from options when the app starts, so
        // tests and environment variables can set the folder.
        services.AddOptions<KeyManagementOptions>().Configure<IOptions<KeyStorageOptions>, ILoggerFactory>((options, storage, loggerFactory) =>
        {
            if (!string.IsNullOrWhiteSpace(storage.Value.KeysDirectory))
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(storage.Value.KeysDirectory), loggerFactory);
            }
        });

        return services;
    }

    public static void WarnIfKeysAreNotPersisted(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<IOptions<KeyStorageOptions>>().Value;
        if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(storage.KeysDirectory))
        {
            LogKeysNotPersisted(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DataProtection:KeysDirectory is not set. In a container, the session keys " +
        "are lost on restart and everyone is signed out. Point it at persistent storage")]
    private static partial void LogKeysNotPersisted(ILogger logger);
}
