using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>
/// POST /api/claims: FNOL (FRS §5, §10.1). Everything is created in one transaction: the claim number
/// (D-10), the claim with its loss event, parties and risk objects, the completeness issues (D-06/D-07),
/// the optional initial reserve and the audit rows. <see cref="PolicyId"/> null is the "Unknown policy"
/// intake (FRS §5.2 step 1).
/// </summary>
public sealed record CreateClaimCommand(
    Guid? PolicyId,
    DateTimeOffset? LossDate,
    string? LossDescription,
    string? LossLocation,
    string? CauseOfLossCode,
    decimal? EstimatedLossAmount,
    string? PoliceReportNumber,
    ClaimSeverity? Severity,
    IReadOnlyList<PartyInput>? Parties,
    IReadOnlyList<RiskObjectInput>? RiskObjects,
    InitialReserveInput? InitialReserve) : ICommand<ClaimCreatedDto>;

/// <summary>
/// The 201 body of POST /api/claims: the new claim number and status, the issues it was created with
/// (D-06: the FE shows them on the detail screen), and the initial reserve's outcome.
/// </summary>
public sealed record ClaimCreatedDto(
    Guid Id,
    string ClaimNumber,
    ClaimStatus Status,
    IReadOnlyList<ValidationIssueDto> ValidationIssues,
    ReserveSubmittedDto? InitialReserve);

