using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

public sealed class KeyStorageTests(OpsHubApiFactory baseFactory) : IClassFixture<OpsHubApiFactory>, IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("opshub-keys-");

    private DirectoryInfo KeysFolder => _work.CreateSubdirectory("keys");

    [Fact]
    public async Task SessionKeys_AreWrittenToTheConfiguredFolder()
    {
        using var factory = WithKeys(KeysFolder.FullName);
        using var client = factory.CreateDefaultClient(OpsHubApiFactory.BaseAddress);

        // Issuing an XSRF token needs a key, so the first one is created and saved.
        using var response = await client.GetPathAsync("/api/auth/antiforgery");
        response.EnsureSuccessStatusCode();

        Assert.NotEmpty(KeysFolder.GetFiles("key-*.xml"));
    }

    [Fact]
    public async Task WithACertificate_KeysAreEncryptedOnDisk()
    {
        var (path, password) = CreateCertificate();
        using var factory = WithKeys(KeysFolder.FullName, path, password);
        using var client = factory.CreateDefaultClient(OpsHubApiFactory.BaseAddress);

        using var response = await client.GetPathAsync("/api/auth/antiforgery");
        response.EnsureSuccessStatusCode();

        var keyXml = await File.ReadAllTextAsync(Assert.Single(KeysFolder.GetFiles("key-*.xml")).FullName, TestContext.Current.CancellationToken);
        Assert.Contains("EncryptedData", keyXml, StringComparison.Ordinal);
        Assert.DoesNotContain("unencrypted form", keyXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EncryptedKeys_StillWorkAfterARestart()
    {
        var (path, password) = CreateCertificate();
        var cookies = new CookieContainer();

        // First run: a key is created and encrypted; the browser gets an XSRF cookie and token.
        using (var firstRun = WithKeys(KeysFolder.FullName, path, password))
        using (var client = firstRun.CreateDefaultClient(OpsHubApiFactory.BaseAddress, new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies)))
        using (var response = await client.GetPathAsync("/api/auth/antiforgery"))
        {
            response.EnsureSuccessStatusCode();
        }

        // Second run reads the key from disk, which means decrypting it. The token from the first
        // run still validates (so the sign-in is judged on the password: 401, not 400), and no
        // replacement key was needed.
        using var secondRun = WithKeys(KeysFolder.FullName, path, password);
        using var browser = secondRun.CreateDefaultClient(OpsHubApiFactory.BaseAddress, new XsrfHeaderHandler(cookies), new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies));
        using var login = await browser.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative),
            new { email = "nobody@tests.example", password = "wrong-password-123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Single(KeysFolder.GetFiles("key-*.xml"));
    }

    [Fact]
    public async Task WithoutTheOriginalCertificate_OldSessionsAndTokensStopWorking()
    {
        // Why the certificate must be backed up: a different one can't decrypt the stored key.
        var (originalPath, originalPassword) = CreateCertificate("original.pfx");
        var (otherPath, otherPassword) = CreateCertificate("other.pfx");
        var cookies = new CookieContainer();

        using (var firstRun = WithKeys(KeysFolder.FullName, originalPath, originalPassword))
        using (var client = firstRun.CreateDefaultClient(OpsHubApiFactory.BaseAddress, new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies)))
        using (var response = await client.GetPathAsync("/api/auth/antiforgery"))
        {
            response.EnsureSuccessStatusCode();
        }

        using var secondRun = WithKeys(KeysFolder.FullName, otherPath, otherPassword);
        using var browser = secondRun.CreateDefaultClient(OpsHubApiFactory.BaseAddress, new XsrfHeaderHandler(cookies), new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies));
        using var login = await browser.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative),
            new { email = "nobody@tests.example", password = "wrong-password-123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode); // the old XSRF token can't be validated
    }

    [Fact]
    public void AWrongCertificatePassword_StopsTheAppFromStarting()
    {
        var (path, _) = CreateCertificate();
        using var factory = WithKeys(KeysFolder.FullName, path, "not-the-password");

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("DataProtection:CertificatePath could not be loaded", exception.ToString(), StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> WithKeys(string folder, string? certificatePath = null, string? password = null) =>
        baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("DataProtection:KeysDirectory", folder);
            b.UseSetting("DataProtection:CertificatePath", certificatePath);
            b.UseSetting("DataProtection:CertificatePassword", password);
        });

    /// <summary>A throwaway self-signed certificate, like one made with openssl for a real installation.</summary>
    private (string Path, string Password) CreateCertificate(string fileName = "keys.pfx")
    {
        var password = $"test-{Guid.NewGuid():N}";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=OpsHub test key protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));

        var path = Path.Combine(_work.FullName, fileName);
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        return (path, password);
    }

    public void Dispose() => _work.Delete(recursive: true);
}
