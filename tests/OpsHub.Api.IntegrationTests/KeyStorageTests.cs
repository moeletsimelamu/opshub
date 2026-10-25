using Microsoft.AspNetCore.Hosting;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

public class KeyStorageTests(OpsHubApiFactory baseFactory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task SessionKeys_AreWrittenToTheConfiguredFolder()
    {
        var folder = Directory.CreateTempSubdirectory("opshub-keys-");
        try
        {
            using var factory = baseFactory.WithWebHostBuilder(b => b.UseSetting("DataProtection:KeysDirectory", folder.FullName));
            using var client = factory.CreateDefaultClient(OpsHubApiFactory.BaseAddress);

            // Issuing an XSRF token needs a key, so the first one is created and saved.
            using var response = await client.GetPathAsync("/api/auth/antiforgery");
            response.EnsureSuccessStatusCode();

            Assert.NotEmpty(folder.GetFiles("key-*.xml"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
