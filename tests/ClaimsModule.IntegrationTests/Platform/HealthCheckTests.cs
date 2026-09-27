using System.Net;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Platform;

[Collection(ApiCollection.Name)]
public sealed class HealthCheckTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task OPS_01_Health_endpoints_report_healthy_without_a_token(string path)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
