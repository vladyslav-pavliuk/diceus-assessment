using ClaimsModule.API.Auth;
using ClaimsModule.Application.Policies;
using ClaimsModule.Application.Policies.Queries;
using ClaimsModule.Application.ReferenceData;
using ClaimsModule.Application.ReferenceData.Queries;
using ClaimsModule.Application.Users;
using ClaimsModule.Application.Users.Queries.ListUsers;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

/// <summary>Reference data (FRS §10.3).</summary>
[ApiController]
[Route("api/reference")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ReferenceDataController(ISender sender) : ControllerBase
{
    [HttpGet("cause-of-loss-codes")]
    [ProducesResponseType<IReadOnlyList<CauseOfLossCodeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CauseOfLossCodeDto>>> ListCauseOfLossCodes(
        PerilCategory? perilCategory, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListCauseOfLossCodesQuery(perilCategory), cancellationToken));

    [HttpGet("claim-statuses")]
    [ProducesResponseType<IReadOnlyList<ClaimStatusDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClaimStatusDto>>> ListClaimStatuses(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListClaimStatusesQuery(), cancellationToken));
}

/// <summary>The simulated policy lookup (FRS §5.5, §10.3; brief §3.3.2).</summary>
[ApiController]
[Route("api/policies")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class PoliciesController(ISender sender) : ControllerBase
{
    /// <summary>Policies whose number or client name contains <paramref name="q"/>.</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<PolicyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<PolicyDto>>> Search(string? q, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListPoliciesQuery(q), cancellationToken));

    [HttpGet("{id:guid}/coverage")]
    [ProducesResponseType<PolicyCoverageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolicyCoverageDto>> GetCoverage(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPolicyCoverageQuery(id), cancellationToken));
}

/// <summary>Users of the caller's organisation (D-29 handler filter, D-18 assign dialog).</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(UserRole? role, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListUsersQuery(role), cancellationToken));
}
