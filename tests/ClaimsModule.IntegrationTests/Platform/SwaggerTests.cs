using System.Text.Json;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Platform;

[Collection(ApiCollection.Name)]
public sealed class SwaggerTests(ApiFixture fixture)
{
    [Fact]
    public async Task API_DOCS_OpenAPI_document_declares_the_JWT_bearer_scheme()
    {
        var json = await fixture.Factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
        var document = JsonDocument.Parse(json).RootElement;

        var bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        document.GetProperty("security").GetArrayLength().ShouldBe(1);
        document.GetProperty("paths").TryGetProperty("/api/auth/dev-token", out _).ShouldBeTrue();
    }
}
