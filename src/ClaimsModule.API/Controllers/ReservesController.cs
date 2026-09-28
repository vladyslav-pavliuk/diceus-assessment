using ClaimsModule.API.Auth;
using ClaimsModule.API.Contracts.Claims;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Commands.AdjustReserve;
using ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;
using ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;
using ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;
using ClaimsModule.Application.Claims.Commands.RetryGlPosting;
using ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;
using ClaimsModule.Application.Claims.Queries.GetClaimReserves;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

/// <summary>
/// Approve and reject are gated to supervisors here (403, D-25); the aggregate re-checks self-approval, the amount tier
/// and the $10M limit (422).
/// </summary>
[ApiController]
[Route("api/claims/{claimId:guid}/reserves")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ReservesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClaimReservesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimReservesDto>> Get(Guid claimId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetClaimReservesQuery(claimId), cancellationToken));

    [HttpPost]
    [ProducesResponseType<ReserveSubmittedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveSubmittedDto>> Submit(Guid claimId, SubmitReserveRequest request, CancellationToken cancellationToken)
    {
        var submitted = await sender.Send(
            new SubmitReserveTransactionCommand(claimId, request.Component, request.TransactionType, request.Amount, request.ChangeReason),
            cancellationToken);

        // No GET for a single transaction: it is read as part of GET /reserves.
        return StatusCode(StatusCodes.Status201Created, submitted);
    }

    /// <summary>Sets an absolute balance, submitted as an Adjust for the difference (D-04).</summary>
    [HttpPut("{componentId:guid}")]
    [ProducesResponseType<ReserveSubmittedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveSubmittedDto>> Adjust(
        Guid claimId, Guid componentId, AdjustReserveRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new AdjustReserveCommand(claimId, componentId, request.NewAmount, request.ChangeReason), cancellationToken));

    [HttpPost("{transactionId:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.Supervisor)]
    [ProducesResponseType<ReserveTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveTransactionDto>> Approve(Guid claimId, Guid transactionId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ApproveReserveTransactionCommand(claimId, transactionId), cancellationToken));

    [HttpPost("{transactionId:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.Supervisor)]
    [ProducesResponseType<ReserveTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveTransactionDto>> Reject(
        Guid claimId, Guid transactionId, RejectReserveRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RejectReserveTransactionCommand(claimId, transactionId, request.RejectionReason), cancellationToken));

    [HttpPost("{transactionId:guid}/retract")]
    [ProducesResponseType<ReserveTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveTransactionDto>> Retract(Guid claimId, Guid transactionId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RetractReserveTransactionCommand(claimId, transactionId), cancellationToken));

    /// <summary>202, because the posting itself happens later in the GL job.</summary>
    [HttpPost("{transactionId:guid}/retry-posting")]
    [ProducesResponseType<ReserveTransactionDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveTransactionDto>> RetryPosting(Guid claimId, Guid transactionId, CancellationToken cancellationToken) =>
        Accepted(await sender.Send(new RetryGlPostingCommand(claimId, transactionId), cancellationToken));
}
