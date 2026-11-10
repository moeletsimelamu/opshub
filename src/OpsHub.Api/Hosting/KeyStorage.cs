using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

    /// <summary>
    /// Optional .pfx certificate (with its private key) that encrypts the keys before they are
    /// written. Without it the keys are stored as plain XML, and anyone who can read the folder
    /// could forge a session cookie. Keep it outside the keys folder, and back it up separately.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>The certificate file's password. A secret: pass it as an environment variable.</summary>
    public string? CertificatePassword { get; set; }
}

internal static partial class KeyStorage
{
    public static IServiceCollection AddOpsHubKeyStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(KeyStorageOptions.SectionName);
        services.Configure<KeyStorageOptions>(section);

        // A fixed name, so keys stay valid when the app is installed in a different folder.
        var dataProtection = services.AddDataProtection().SetApplicationName("OpsHub");

        // What PersistKeysToFileSystem does, but read from options when the app starts, so
        // tests and environment variables can set the folder.
        services.AddOptions<KeyManagementOptions>().Configure<IOptions<KeyStorageOptions>, ILoggerFactory>((options, storage, loggerFactory) =>
        {
            if (!string.IsNullOrWhiteSpace(storage.Value.KeysDirectory))
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(storage.Value.KeysDirectory), loggerFactory);
            }
        });

        // Loaded now, while the app starts: a wrong path or password stops it here with a clear
        // message, instead of failing at somebody's first sign-in.
        if (LoadCertificate(section.Get<KeyStorageOptions>() ?? new KeyStorageOptions()) is { } certificate)
        {
            dataProtection.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
        }

        return services;
    }

    private static X509Certificate2? LoadCertificate(KeyStorageOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CertificatePath))
        {
            return null;
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, settings.CertificatePassword);
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"DataProtection:CertificatePath could not be loaded ({e.GetType().Name}). Check the file and DataProtection:CertificatePassword.", e);
        }

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("The DataProtection certificate has no private key, so the keys could not be decrypted again.");
        }

        return certificate;
    }

    public static void CheckKeyStorage(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<IOptions<KeyStorageOptions>>().Value;
        if (app.Environment.IsDevelopment())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(storage.KeysDirectory))
        {
            LogKeysNotPersisted(app.Logger);
        }
        else if (string.IsNullOrWhiteSpace(storage.CertificatePath))
        {
            LogKeysNotEncrypted(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DataProtection:KeysDirectory is not set. In a container, the session keys " +
        "are lost on restart and everyone is signed out. Point it at persistent storage")]
    private static partial void LogKeysNotPersisted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DataProtection:CertificatePath is not set, so the session keys are stored " +
        "unencrypted. Protect the keys folder and its backups, or set a certificate (docs/DEPLOYMENT.md)")]
    private static partial void LogKeysNotEncrypted(ILogger logger);
}
