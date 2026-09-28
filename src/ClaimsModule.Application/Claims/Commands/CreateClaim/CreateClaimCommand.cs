using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>FNOL in one transaction. A null <see cref="PolicyId"/> is the "Unknown policy" intake.</summary>
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

public sealed record ClaimCreatedDto(
    Guid Id,
    string ClaimNumber,
    ClaimStatus Status,
    IReadOnlyList<ValidationIssueDto> ValidationIssues,
    ReserveSubmittedDto? InitialReserve);

