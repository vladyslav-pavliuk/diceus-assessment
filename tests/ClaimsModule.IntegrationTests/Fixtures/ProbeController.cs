using ClaimsModule.API.Auth;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>Test-only endpoints, registered by <see cref="ClaimsApiFactory"/> and never shipped.</summary>
[ApiController]
[Route("test/probe")]
public sealed class ProbeController(ICurrentUser currentUser, ICorrelationContext correlationContext) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me() => Ok(new ProbeUser(currentUser.UserId, currentUser.DisplayName, currentUser.Role?.ToString(), currentUser.OrganisationId));

    [HttpGet("handler")]
    [Authorize(Policy = AuthorizationPolicies.Handler)]
    public IActionResult HandlerOnly() => Ok();

    [HttpGet("supervisor")]
    [Authorize(Policy = AuthorizationPolicies.Supervisor)]
    public IActionResult SupervisorOnly() => Ok();

    [HttpGet("manager")]
    [Authorize(Policy = AuthorizationPolicies.Manager)]
    public IActionResult ManagerOnly() => Ok();

    [AllowAnonymous]
    [HttpGet("correlation")]
    public IActionResult Correlation() => Ok(new ProbeCorrelation(correlationContext.CorrelationId));

    [AllowAnonymous]
    [HttpGet("throw/{kind}")]
    public IActionResult Throw(string kind) => kind switch
    {
        "business-rule" => throw new BusinessRuleViolationException("ApprovedByUserId", "Self-approval is not permitted."),
        "not-found" => throw new NotFoundException("Claim", "CLM-2026-0000001"),
        "forbidden" => throw new ForbiddenAccessException("Only a manager can set the reserve limit override."),
        "concurrency" => throw new DbUpdateConcurrencyException("Row version mismatch."),
        _ => throw new InvalidOperationException("Sensitive internal detail."),
    };

    public sealed record ProbeUser(Guid? UserId, string? DisplayName, string? Role, Guid? OrganisationId);

    public sealed record ProbeCorrelation(string CorrelationId);
}
