using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Claims;

/// <summary>POST /api/claims (FRS §5, §10.1) through HTTP: the happy path and every rule that answers 422 (D-06).</summary>
[Collection(ApiCollection.Name)]
public sealed class CreateClaimTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _api = null!;

    public async Task InitializeAsync() => _api = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_01_Create_claim_returns_201_with_claim_number_and_location()
    {
        var response = await _api.PostClaimAsync(await _api.ValidFnolAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<ClaimCreatedDto>(TestAuth.Json))!;
        ClaimNumber.TryParse(created.ClaimNumber, out var number).ShouldBeTrue(); // BR-C-04 format
        number!.Year.ShouldBe(DateTimeOffset.UtcNow.Year);
        created.Status.ShouldBe(ClaimStatus.Draft);
        created.ValidationIssues.ShouldBeEmpty(); // complete intake: nothing to fix
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/claims/{created.Id}");

        var detail = await _api.GetDetailAsync(created.Id);
        detail.ClaimNumber.ShouldBe(created.ClaimNumber);
        detail.PolicyNumber.ShouldBe(ClaimsApi.InForcePolicy);
        detail.ClientName.ShouldBe("Northwind Logistics Ltd");
        detail.LossEvent.CauseOfLossName.ShouldBe("Vehicle Collision");
        detail.Parties.ShouldHaveSingleItem().DisplayName.ShouldBe("Jordan Reyes");
        detail.RiskObjects.ShouldHaveSingleItem().IsPrimary.ShouldBeTrue();
        detail.AssignedHandlerName.ShouldBe("Alex Carter"); // D-18: the creator
    }

    [Fact]
    public async Task AUD_01_Creation_writes_the_audit_trail_with_the_request_correlation_id()
    {
        var correlationId = Guid.NewGuid();
        var response = await _api.PostClaimAsync(await _api.ValidFnolAsync(), correlationId: correlationId);
        var created = (await response.Content.ReadFromJsonAsync<ClaimCreatedDto>(TestAuth.Json))!;

        var audit = await _api.AuditAsync(created.Id);
        audit.Select(entry => entry.EventType).ShouldBe(
            [AuditEventTypes.ClaimCreated, AuditEventTypes.PartyAdded, AuditEventTypes.RiskObjectAdded],
            ignoreOrder: true);
        audit.ShouldAllBe(entry => entry.CorrelationId == correlationId); // API-CORR / AUD-I3 over HTTP
        audit.ShouldAllBe(entry => entry.CreatedByName == "Alex Carter");
        audit.Single(entry => entry.EventType == AuditEventTypes.ClaimCreated).NewValue.ShouldBe($"{{\"claimNumber\":\"{created.ClaimNumber}\"}}");
    }

    [Fact]
    public async Task API_01_Initial_reserve_within_authority_is_auto_approved_at_intake()
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = 10_000m };

        var created = await _api.CreateClaimAsync(body);

        var reserve = created.InitialReserve.ShouldNotBeNull();
        reserve.Component.ShouldBe(ReserveComponentType.Indemnity);
        reserve.Transaction.TransactionType.ShouldBe(ReserveTransactionType.Add);
        reserve.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved); // BR-R-02: exactly $10,000
        reserve.Transaction.ChangeReason.ShouldBe("Initial reserve at FNOL.");
        reserve.Transaction.PostingStatus.ShouldBe(PostingStatus.Pending); // the GL job arrives in Phase 4
        reserve.Warnings.ShouldBeEmpty();

        (await _api.GetDetailAsync(created.Id)).TotalReserves.ShouldBe(10_000m);
        (await _api.AuditAsync(created.Id)).Select(entry => entry.EventType)
            .ShouldContain(AuditEventTypes.ReserveCreated);
        (await _api.AuditAsync(created.Id)).Select(entry => entry.EventType)
            .ShouldContain(AuditEventTypes.ReserveAutoApproved);
    }

    [Fact]
    public async Task BR_R_02_Initial_reserve_above_10000_waits_for_a_supervisor()
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Expense", ["amount"] = 10_000.01m, ["changeReason"] = "Expert fees" };

        var reserve = (await _api.CreateClaimAsync(body)).InitialReserve!;

        reserve.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        reserve.Transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Supervisor);
        reserve.Transaction.ChangeReason.ShouldBe("Expert fees");
    }

    [Fact]
    public async Task BR_R_05_Initial_reserve_above_10M_warns_and_escalates_to_manager()
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = 10_000_000.01m };

        var reserve = (await _api.CreateClaimAsync(body)).InitialReserve!;

        reserve.Warnings.ShouldBe(["Total reserves will exceed $10,000,000. Manager override required."]);
        reserve.Transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Manager);
    }

    [Fact]
    public async Task BR_C_01_Future_loss_date_returns_422_with_the_FRS_message()
    {
        var body = await _api.ValidFnolAsync();
        body["lossDate"] = DateTimeOffset.UtcNow.AddHours(2).ToString("O");

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["LossDate"].ShouldBe(["Loss date cannot be in the future."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public async Task VAL_02_Missing_or_malformed_loss_date_returns_loss_date_is_required(string? lossDate)
    {
        var body = await _api.ValidFnolAsync();
        body["lossDate"] = lossDate;

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["LossDate"].ShouldBe(["Loss date is required."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Too short")]
    [InlineData("   Nineteen chars!!   ")]
    public async Task BR_C_07_Description_under_20_characters_returns_422(string? description)
    {
        var body = await _api.ValidFnolAsync();
        body["lossDescription"] = description;

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["LossDescription"]
            .ShouldBe(["Loss description is required and must be at least 20 characters."]);
    }

    [Theory]
    [InlineData("COL-NOPE")]
    [InlineData("col-fire")] // codes are exact identifiers, although SQL Server compares case-insensitively
    [InlineData("")]
    public async Task BR_C_05_Unknown_cause_code_returns_422(string code)
    {
        var body = await _api.ValidFnolAsync();
        body["causeOfLossCode"] = code;

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["CauseOfLossCode"]
            .ShouldBe(["Cause of loss code is not recognised or is inactive."]);
    }

    [Fact]
    public async Task BR_C_05_Inactive_cause_code_returns_422()
    {
        // Every seeded code is active (FRS §5.6), so one is switched off for the duration of the test.
        // Tests in the collection run one at a time, so no other test sees the change.
        await SetActiveAsync("COL-OTHER", isActive: false);
        try
        {
            var body = await _api.ValidFnolAsync();
            body["causeOfLossCode"] = "COL-OTHER";

            (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["CauseOfLossCode"]
                .ShouldBe(["Cause of loss code is not recognised or is inactive."]);
        }
        finally
        {
            await SetActiveAsync("COL-OTHER", isActive: true);
        }
    }

    [Fact]
    public async Task BR_C_02_Expired_policy_POL_2023_000099_creates_draft_with_warning()
    {
        var created = await _api.CreateClaimAsync(await _api.ValidFnolAsync(ClaimsApi.ExpiredPolicy));

        created.Status.ShouldBe(ClaimStatus.Draft);
        var issue = created.ValidationIssues.ShouldHaveSingleItem();
        issue.RuleCode.ShouldBe("BR-C-02");
        issue.Severity.ShouldBe(IssueSeverity.Warning);
        issue.Message.ShouldBe("Loss date is outside the policy effective period.");

        (await _api.AuditAsync(created.Id)).ShouldContain(entry => entry.EventType == AuditEventTypes.ValidationIssueAdded); // AUD-16
    }

    [Fact]
    public async Task BR_C_03_Claim_without_claimant_is_created_in_draft_with_a_critical_issue()
    {
        var body = await _api.ValidFnolAsync();
        body["parties"] = new JsonArray(ClaimsApi.ClaimantPerson(role: "Witness"));

        var created = await _api.CreateClaimAsync(body);

        created.Status.ShouldBe(ClaimStatus.Draft);
        var issue = created.ValidationIssues.ShouldHaveSingleItem();
        issue.RuleCode.ShouldBe("BR-C-03");
        issue.Severity.ShouldBe(IssueSeverity.Critical);
        issue.Message.ShouldBe("At least one Claimant party is required to open a claim.");
    }

    [Fact]
    public async Task BR_C_06_Unknown_policy_intake_creates_warnings_for_policy_and_risk_objects()
    {
        var body = ClaimsApi.FnolBody(policyId: null);
        body.Remove("riskObjects");

        var created = await _api.CreateClaimAsync(body);

        created.ValidationIssues.Select(issue => (issue.RuleCode, issue.Message)).ShouldBe(
            [
                ("BR-C-06", "No policy linked. Policy must be associated before reserves can be set."),
                ("NO-RISK-OBJECT", "No risk objects linked."),
            ],
            ignoreOrder: true);
        (await _api.GetDetailAsync(created.Id)).PolicyNumber.ShouldBeNull();
    }

    [Fact]
    public async Task BR_C_06_Initial_reserve_without_policy_returns_422()
    {
        var body = ClaimsApi.FnolBody(policyId: null);
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = 500m };

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["PolicyId"]
            .ShouldBe(["No policy linked. Policy must be associated before reserves can be set."]);
    }

    [Fact]
    public async Task API_01_Unknown_policy_id_returns_422()
    {
        var body = ClaimsApi.FnolBody(Guid.NewGuid());

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["PolicyId"].ShouldBe(["Policy was not found."]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task VAL_08_Initial_reserve_amount_not_above_zero_returns_422(decimal amount)
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = amount };

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["InitialReserve.Amount"]
            .ShouldBe(["Reserve amount must be greater than zero."]);
    }

    [Fact]
    public async Task BR_R_01_Negative_initial_subrogation_is_accepted()
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "SubrogationRecoverable", ["amount"] = -2_500m };

        (await _api.CreateClaimAsync(body)).InitialReserve!.Transaction.Amount.ShouldBe(-2_500m);
    }

    [Theory]
    [InlineData("Litigation")]
    [InlineData("3")]
    [InlineData(null)]
    public async Task VAL_09_Invalid_reserve_component_returns_422(string? component)
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = component, ["amount"] = 100m };

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body)))["InitialReserve.Component"]
            .ShouldBe(["Invalid reserve component type."]);
    }

    [Fact]
    public async Task BR_P_02_Unknown_party_role_and_incomplete_party_return_422_per_field()
    {
        var body = await _api.ValidFnolAsync();
        body["parties"] = new JsonArray(
            new JsonObject { ["role"] = "Spectator", ["type"] = "Person", ["firstName"] = "Pat", ["lastName"] = "Doe" },
            new JsonObject { ["role"] = "Insured", ["type"] = "Company", ["email"] = "not-an-email" });

        var errors = await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body));

        errors["Parties[0].Role"].ShouldBe(["Invalid party role."]);
        errors["Parties[1].CompanyName"].ShouldBe(["Company name is required for a company."]);
        errors["Parties[1].Email"].ShouldBe(["Email address is not valid."]);
    }

    /// <summary>FRS §10.4: every failed rule of one request comes back in one 422.</summary>
    [Fact]
    public async Task API_ERR_Several_failures_are_reported_together()
    {
        var body = await _api.ValidFnolAsync();
        body["lossDate"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        body["lossDescription"] = "Short";
        body["riskObjects"] = new JsonArray(new JsonObject { ["assetType"] = "Spaceship" });

        var errors = await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body));

        errors.Keys.ShouldBe(["LossDate", "LossDescription", "RiskObjects[0].AssetType", "RiskObjects[0].AssetDescription"], ignoreOrder: true);
    }

    /// <summary>Over-long text is a 422 with a message, never a SQL truncation error (500).</summary>
    [Fact]
    public async Task API_01_Values_longer_than_their_column_return_422()
    {
        var body = await _api.ValidFnolAsync();
        body["lossLocation"] = new string('x', 501);
        body["parties"] = new JsonArray(new JsonObject
        {
            ["role"] = "Claimant", ["type"] = "Person", ["firstName"] = new string('a', 101), ["lastName"] = "Reyes", ["phone"] = new string('9', 51),
        });

        var errors = await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(body));

        errors["LossLocation"].ShouldBe(["Loss location must not exceed 500 characters."]);
        errors["Parties[0].FirstName"].ShouldBe(["First name must not exceed 100 characters."]);
        errors["Parties[0].Phone"].ShouldBe(["Phone must not exceed 50 characters."]);
    }

    [Fact]
    public async Task API_AUTH_Creating_a_claim_needs_a_token()
    {
        var anonymous = fixture.Factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/claims", ClaimsApi.FnolBody(null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task SetActiveAsync(string code, bool isActive)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE [CauseOfLossCodes] SET [IsActive] = {isActive} WHERE [Code] = {code}");
    }
}
