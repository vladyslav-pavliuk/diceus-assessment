using ClaimsModule.API.Auth;
using ClaimsModule.API.Contracts.Claims;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Commands.AssignClaimHandler;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Commands.LinkPolicy;
using ClaimsModule.Application.Claims.Commands.SetReserveLimitOverride;
using ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;
using ClaimsModule.Application.Claims.Commands.UpdateClaimDetails;
using ClaimsModule.Application.Claims.Commands.ValidateClaim;
using ClaimsModule.Application.Claims.Queries;
using ClaimsModule.Application.Claims.Queries.GetClaimAudit;
using ClaimsModule.Application.Claims.Queries.GetClaimDetail;
using ClaimsModule.Application.Claims.Queries.ListClaims;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Route("api/claims")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ClaimsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ClaimCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClaimCreatedDto>> Create(CreateClaimCommand command, CancellationToken cancellationToken)
    {
        var created = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary><c>status</c> may repeat.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClaimSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<ClaimSummaryDto>>> List(
        [FromQuery(Name = "status")] ClaimStatus[]? statuses,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        Guid? assignedHandlerId,
        string? causeOfLossCode,
        Guid? policyId,
        string? search,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = ClaimQueryLimits.DefaultPageSize) =>
        Ok(await sender.Send(
            new ListClaimsQuery(statuses, dateFrom, dateTo, assignedHandlerId, causeOfLossCode, policyId, search, page, pageSize),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClaimDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetClaimDetailQuery(id), cancellationToken));

    /// <summary>422 lists the valid next statuses or every blocking condition.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<ClaimStatusChangedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClaimStatusChangedDto>> TransitionStatus(
        Guid id, TransitionClaimStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new TransitionClaimStatusCommand(id, request.TargetStatus, request.Reason, request.Justification), cancellationToken));

    [HttpGet("{id:guid}/audit")]
    [ProducesResponseType<PagedResult<AuditEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> GetAudit(
        Guid id, CancellationToken cancellationToken, int page = 1, int pageSize = ClaimQueryLimits.DefaultAuditPageSize) =>
        Ok(await sender.Send(new GetClaimAuditQuery(id, page, pageSize), cancellationToken));

    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType<IReadOnlyList<ValidationIssueDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ValidationIssueDto>>> Validate(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ValidateClaimCommand(id), cancellationToken));

    [HttpPut("{id:guid}/policy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LinkPolicy(Guid id, LinkPolicyRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new LinkPolicyCommand(id, request.PolicyId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = AuthorizationPolicies.Supervisor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignHandler(Guid id, AssignClaimHandlerRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new AssignClaimHandlerCommand(id, request.UserId), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDetails(Guid id, UpdateClaimDetailsRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateClaimDetailsCommand(id, request.Notes, request.Severity), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/reserve-limit-override")]
    [Authorize(Policy = AuthorizationPolicies.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetReserveLimitOverride(Guid id, SetReserveLimitOverrideRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetReserveLimitOverrideCommand(id, request.Enabled, request.Reason), cancellationToken);
        return NoContent();
    }
}
