using System.Net.Http.Json;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Middleware;

[Collection(ApiCollection.Name)]
public sealed class CorrelationIdTests(ApiFixture fixture)
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task API_CORR_Incoming_correlation_id_is_used_and_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/probe/correlation");
        const string incoming = "5c1f3a0e-8d7b-4c2a-9f61-2b7d3e4a5f60";
        request.Headers.Add(Header, incoming);

        var response = await fixture.Factory.CreateClient().SendAsync(request);

        response.Headers.GetValues(Header).ShouldHaveSingleItem().ShouldBe(incoming);
        (await response.Content.ReadFromJsonAsync<ProbeController.ProbeCorrelation>())!.CorrelationId.ShouldBe(incoming);
    }

    [Fact]
    public async Task API_CORR_Missing_correlation_id_is_generated()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/test/probe/correlation");

        var generated = response.Headers.GetValues(Header).ShouldHaveSingleItem();
        Guid.TryParse(generated, out _).ShouldBeTrue();
        (await response.Content.ReadFromJsonAsync<ProbeController.ProbeCorrelation>())!.CorrelationId.ShouldBe(generated);
    }

    /// <summary>ClaimAuditLog.CorrelationId is a GUID (FRS §9.8), so non-GUID ids are replaced (D-39 Q1).</summary>
    [Theory]
    [InlineData("fnol-intake-42")]
    [InlineData("{5c1f3a0e-8d7b-4c2a-9f61-2b7d3e4a5f60}")]
    [InlineData("contains spaces")]
    [InlineData("semi;colon")]
    [InlineData("a-value-that-is-far-too-long-to-be-accepted-as-a-correlation-identifier")]
    public async Task API_CORR_Non_guid_correlation_id_is_replaced(string unsafeValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/probe/correlation");
        request.Headers.TryAddWithoutValidation(Header, unsafeValue);

        var response = await fixture.Factory.CreateClient().SendAsync(request);

        var echoed = response.Headers.GetValues(Header).ShouldHaveSingleItem();
        echoed.ShouldNotBe(unsafeValue);
        Guid.TryParse(echoed, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task API_CORR_Error_responses_carry_the_correlation_id()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/test/probe/throw/not-found");

        response.Headers.Contains(Header).ShouldBeTrue();
    }
}
