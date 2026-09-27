using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Claims;

/// <summary>
/// The claim aggregate root (FRS §9.1). It owns the loss event, parties, risk objects, validation
/// issues, reserve components with their transactions, and document metadata
/// (ARCHITECTURE-PLAN §2.1). Every state change goes through a method here, so the status machine,
/// the closure conditions and the reserve invariants are checked against one consistent snapshot.
/// <para>
/// The class is split by concern: this file (creation, details, documents), Claim.Status.cs,
/// Claim.Parties.cs (parties, risk objects, validation issues) and Claim.Reserves.cs.
/// </para>
/// </summary>
public sealed partial class Claim : AggregateRoot
{
    private readonly List<ClaimParty> _parties = [];
    private readonly List<ClaimRiskObject> _riskObjects = [];
    private readonly List<ClaimValidationIssue> _validationIssues = [];
    private readonly List<ReserveComponent> _reserveComponents = [];
    private readonly List<ClaimDocument> _documents = [];

    private Claim()
    {
    }

    private Claim(Guid id)
        : base(id)
    {
    }

    public string ClaimNumber { get; private set; } = null!;

    public Guid? PolicyId { get; private set; }

    /// <summary>Denormalised from the linked policy for display (FRS §9.1); null without a policy (D-33).</summary>
    public string? PolicyNumber { get; private set; }

    /// <summary>Denormalised from the linked policy for display (FRS §9.1); null without a policy (D-33).</summary>
    public string? ClientName { get; private set; }

    public ClaimStatus Status { get; private set; }

    public ClaimSeverity Severity { get; private set; }

    /// <summary>When the FNOL was received: the server time of creation (D-32).</summary>
    public DateTimeOffset ReportedDate { get; private set; }

    public Guid? AssignedHandlerId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>The closure or withdrawal reason (FRS §9.1, D-26).</summary>
    public string? ClosureReason { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Set by a manager to allow approved reserves above $10,000,000 (BR-R-05, FRS §3).</summary>
    public bool ReserveLimitOverride { get; private set; }

    public string? ReserveLimitOverrideReason { get; private set; }

    public Guid? ReserveLimitOverrideByUserId { get; private set; }

    public DateTimeOffset? ReserveLimitOverrideAt { get; private set; }

    public LossEvent LossEvent { get; private set; } = null!;

    public IReadOnlyList<ClaimParty> Parties => _parties.AsReadOnly();

    public IReadOnlyList<ClaimRiskObject> RiskObjects => _riskObjects.AsReadOnly();

    public IReadOnlyList<ClaimValidationIssue> ValidationIssues => _validationIssues.AsReadOnly();

    public IReadOnlyList<ReserveComponent> ReserveComponents => _reserveComponents.AsReadOnly();

    public IReadOnlyList<ClaimDocument> Documents => _documents.AsReadOnly();

    /// <summary>Closed and Withdrawn claims accept no changes except a reopen (D-26).</summary>
    public bool IsReadOnly => Status is ClaimStatus.Closed or ClaimStatus.Withdrawn;

    /// <summary>
    /// FNOL (FRS §5.2, §10.1 POST /api/claims): creates a Draft claim with its loss event, parties and
    /// risk objects, then records the completeness issues it still has (D-06, D-07). The creator is
    /// the assigned handler (D-18). An initial reserve is submitted afterwards, in the same unit of
    /// work, through <see cref="SubmitReserveTransaction"/>.
    /// </summary>
    public static Claim Create(
        ClaimNumber claimNumber,
        Policy? policy,
        LossEventDetails lossEvent,
        ClaimSeverity severity,
        IReadOnlyCollection<PartyDetails> parties,
        IReadOnlyCollection<RiskObjectDetails> riskObjects,
        Actor creator,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(claimNumber);
        if (!Enum.IsDefined(severity))
        {
            throw new BusinessRuleViolationException(ErrorKeys.Severity, "Invalid claim severity.");
        }

        var claim = new Claim(SequentialGuid.NewGuid())
        {
            ClaimNumber = claimNumber.Value,
            Status = ClaimStatus.Draft,
            Severity = severity,
            ReportedDate = now,
            AssignedHandlerId = creator.UserId,
        };

        claim.LossEvent = LossEvent.Create(claim.Id, lossEvent, now);
        if (policy is not null)
        {
            claim.ApplyPolicy(policy);
        }

        claim.Raise(new ClaimCreated(claim.Id, claim.ClaimNumber));

        foreach (var party in parties)
        {
            claim.AddPartyCore(party);
        }

        foreach (var riskObject in riskObjects)
        {
            claim.AddRiskObjectCore(riskObject);
        }

        claim.EvaluateCompletenessIssues(now);
        claim.EvaluatePolicyPeriodIssue(policy, now);
        return claim;
    }

    /// <summary>D-08 PUT /claims/{id}/policy: links (or changes) the policy and re-evaluates BR-C-02 and BR-C-06.</summary>
    public void LinkPolicy(Policy policy, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        if (PolicyId == policy.Id)
        {
            return;
        }

        var previousPolicyId = PolicyId;
        ApplyPolicy(policy);
        Raise(new PolicyLinked(Id, previousPolicyId, policy.Id, policy.PolicyNumber));

        // An acknowledgement of BR-C-02 was given for the old policy's period, so it does not carry over.
        ResolveIssues(ValidationRuleCodes.LossDateOutsidePolicyPeriod, "The linked policy changed.", now);
        EvaluateCompletenessIssues(now);
        EvaluatePolicyPeriodIssue(policy, now);
    }

    /// <summary>
    /// D-08 POST /claims/{id}/validate: re-runs every rule that is persisted as an issue.
    /// <paramref name="linkedPolicy"/> must be the policy the claim is linked to (null if none).
    /// </summary>
    public void Revalidate(Policy? linkedPolicy, DateTimeOffset now)
    {
        if (linkedPolicy?.Id != PolicyId)
        {
            throw new ArgumentException("The policy passed in is not the policy linked to the claim.", nameof(linkedPolicy));
        }

        EnsureModifiable();
        EvaluateCompletenessIssues(now);
        EvaluatePolicyPeriodIssue(linkedPolicy, now);
    }

    /// <summary>D-18: supervisors and managers (re)assign the handler; the assignee must be an active user.</summary>
    public void AssignHandler(User assignee, Actor actor)
    {
        ArgumentNullException.ThrowIfNull(assignee);
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        if (!actor.Role.IsAtLeast(UserRole.Supervisor))
        {
            throw new ForbiddenAccessException("Only a supervisor or manager can assign the handler.");
        }

        if (!assignee.IsActive)
        {
            throw new BusinessRuleViolationException(ErrorKeys.AssignedHandlerId, DomainMessages.AssigneeMustBeActive);
        }

        if (AssignedHandlerId == assignee.Id)
        {
            return;
        }

        var previous = AssignedHandlerId;
        AssignedHandlerId = assignee.Id;
        Raise(new HandlerAssigned(Id, previous, assignee.Id));
    }

    /// <summary>FRS §11.3 Tab 1: internal notes are editable (D-08).</summary>
    public void UpdateNotes(string? notes, Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        var newNotes = Text.NullIfBlank(notes);
        if (newNotes == Notes)
        {
            return;
        }

        var previous = Notes;
        Notes = newNotes;
        Raise(new ClaimDetailsUpdated(Id, nameof(Notes), previous, newNotes));
    }

    /// <summary>FRS §9.1 Severity; who sets it is NOT SPECIFIED, so any role may (D-33).</summary>
    public void ChangeSeverity(ClaimSeverity severity, Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        if (!Enum.IsDefined(severity))
        {
            throw new BusinessRuleViolationException(ErrorKeys.Severity, "Invalid claim severity.");
        }

        if (severity == Severity)
        {
            return;
        }

        var previous = Severity;
        Severity = severity;
        Raise(new ClaimDetailsUpdated(Id, nameof(Severity), previous.ToString(), severity.ToString()));
    }

    /// <summary>
    /// FRS §13: records an uploaded document. The id is generated by the caller before the upload,
    /// because it is part of the blob path (D-28).
    /// </summary>
    public ClaimDocument AddDocument(
        Guid documentId,
        DocumentType documentType,
        string documentName,
        string blobPath,
        string contentType,
        long fileSizeBytes,
        string? notes,
        Actor uploader,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(uploader);
        EnsureModifiable();

        var document = ClaimDocument.Create(
            documentId, Id, documentType, documentName, blobPath, contentType, fileSizeBytes, notes, uploader.UserId, now);

        _documents.Add(document);
        Raise(new DocumentUploaded(Id, document.Id, document.DocumentName));
        return document;
    }

    private void ApplyPolicy(Policy policy)
    {
        PolicyId = policy.Id;
        PolicyNumber = policy.PolicyNumber;
        ClientName = policy.ClientName;
    }

    private void EnsureModifiable()
    {
        if (IsReadOnly)
        {
            throw new BusinessRuleViolationException(ErrorKeys.Claim, DomainMessages.ClaimIsReadOnly(Status));
        }
    }
}
