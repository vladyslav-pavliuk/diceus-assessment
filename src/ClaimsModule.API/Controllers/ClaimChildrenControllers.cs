using ClaimsModule.API.Auth;
using ClaimsModule.API.Contracts.Claims;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Commands.AcknowledgeValidationIssue;
using ClaimsModule.Application.Claims.Commands.AddParty;
using ClaimsModule.Application.Claims.Commands.AddRiskObject;
using ClaimsModule.Application.Claims.Commands.RemoveParty;
using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Claims.Queries.ListValidationIssues;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Route("api/claims/{claimId:guid}/parties")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ClaimPartiesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ClaimPartyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClaimPartyDto>> Add(Guid claimId, PartyInput party, CancellationToken cancellationToken)
    {
        var added = await sender.Send(
            new AddPartyCommand(
                claimId, party.Role, party.Type, party.FirstName, party.LastName, party.CompanyName, party.Email, party.Phone, party.Notes),
            cancellationToken);

        // No GET for a single party: it is read as part of the claim detail.
        return StatusCode(StatusCodes.Status201Created, added);
    }

    [HttpDelete("{partyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Remove(Guid claimId, Guid partyId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemovePartyCommand(claimId, partyId), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/claims/{claimId:guid}/risk-objects")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ClaimRiskObjectsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RiskObjectDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RiskObjectDto>> Add(Guid claimId, RiskObjectInput riskObject, CancellationToken cancellationToken)
    {
        var added = await sender.Send(
            new AddRiskObjectCommand(
                claimId, riskObject.AssetType, riskObject.AssetDescription, riskObject.DamageDescription, riskObject.AssetReference, riskObject.IsPrimary),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, added);
    }
}

[ApiController]
[Route("api/claims/{claimId:guid}/validation-issues")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ClaimValidationIssuesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ValidationIssueDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ValidationIssueDto>>> List(Guid claimId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListValidationIssuesQuery(claimId), cancellationToken));

    [HttpPost("{issueId:guid}/acknowledge")]
    [ProducesResponseType<ValidationIssueDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ValidationIssueDto>> Acknowledge(
        Guid claimId, Guid issueId, AcknowledgeValidationIssueRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new AcknowledgeValidationIssueCommand(claimId, issueId, request.Note), cancellationToken));
}
