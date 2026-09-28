using System.Net;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Jobs;

/// <summary>Hangfire wiring of the API host: recurring jobs registered at start-up (FRS §12) and the dashboard (D-41).</summary>
[Collection(ApiCollection.Name)]
public sealed class JobRegistrationTests(ApiFixture fixture)
{
    [Fact]
    public void JOB_07_Recurring_jobs_registered_at_startup()
    {
        var recurring = HangfireJobs.RecurringJobs(fixture.Factory.Services).ToDictionary(job => job.Id);

        var sla = recurring[SlaMonitoringJob.RecurringJobId];
        sla.Cron.ShouldBe("*/15 * * * *"); // FRS §12.2
        sla.Job.Type.ShouldBe(typeof(SlaMonitoringJob));

        recurring[GlPostingSweeperJob.RecurringJobId].Cron.ShouldBe("*/5 * * * *"); // D-15
        recurring[IdempotencyCleanupJob.RecurringJobId].Job.Type.ShouldBe(typeof(IdempotencyCleanupJob)); // D-24
    }

    [Fact]
    public async Task OPS_03_Hangfire_dashboard_requires_the_manager_role()
    {
        var anonymous = fixture.Factory.CreateClient();
        (await anonymous.GetAsync("/hangfire")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var supervisor = (await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.casey")).Client;
        (await supervisor.GetAsync("/hangfire")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var manager = (await ClaimsApi.SignInAsync(fixture.Factory, "manager.emery")).Client;
        var page = await manager.GetAsync("/hangfire");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).ShouldContain("Claims Module jobs");
    }

    /// <summary>
    /// A browser cannot send a Bearer header when it navigates, so the first visit may carry the token in the query;
    /// the API then keeps it in an HttpOnly, Secure, SameSite=Strict cookie scoped to /hangfire.
    /// </summary>
    [Fact]
    public async Task OPS_03_Dashboard_token_hand_off_sets_a_scoped_cookie()
    {
        var token = (await fixture.Factory.CreateClient().SignInAsync("manager.finley")).AccessToken;
        var browser = fixture.Factory.CreateClient(new() { HandleCookies = false });

        var first = await browser.GetAsync($"/hangfire?access_token={token}");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = first.Headers.GetValues("Set-Cookie").Single();
        cookie.ShouldStartWith("claims-dashboard-token=");
        cookie.ShouldContain("path=/hangfire");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("httponly");

        var next = new HttpRequestMessage(HttpMethod.Get, "/hangfire/jobs/enqueued");
        next.Headers.Add("Cookie", cookie.Split(';')[0]);
        (await browser.SendAsync(next)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Outside the dashboard, neither the query string nor the cookie is a credential.
        var api = new HttpRequestMessage(HttpMethod.Get, $"/api/claims?access_token={token}");
        api.Headers.Add("Cookie", cookie.Split(';')[0]);
        (await browser.SendAsync(api)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
