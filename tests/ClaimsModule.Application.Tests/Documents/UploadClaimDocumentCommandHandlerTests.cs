using System.Text;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Commands.UploadClaimDocument;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ClaimsModule.Application.Tests.Documents;

/// <summary>
/// DOC-09 (D-42): blob first, metadata second. Nothing is stored for a request the rules refuse; a failed commit removes the
/// blob, unless the row exists after all (a commit whose outcome was unknown), because a missing blob would lose data.
/// </summary>
public sealed class UploadClaimDocumentCommandHandlerTests
{
    private static readonly Guid OrganisationId = Guid.NewGuid();
    private static readonly Actor Uploader = new(Guid.NewGuid(), UserRole.Handler);

    private readonly Claim _claim = NewClaim();
    private readonly FakeClaimQueries _queries = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    public UploadClaimDocumentCommandHandlerTests() => _queries.Status = ClaimStatus.Open;

    [Fact]
    public async Task API_08_Uploads_the_blob_then_records_the_document()
    {
        var uploaded = await HandleAsync(Upload("../../evil/Police Report.pdf"));

        var (path, contentType) = _storage.Uploads.ShouldHaveSingleItem();
        path.ShouldBe($"{OrganisationId}/{_claim.Id}/{uploaded.Id}_Police Report.pdf"); // BR-D-01
        contentType.ShouldBe("application/pdf");
        _claim.Documents.ShouldHaveSingleItem().BlobPath.ShouldBe(path);
        uploaded.DocumentName.ShouldBe("Police Report.pdf");
        uploaded.DocumentType.ShouldBe(DocumentType.Other); // D-42 default
        uploaded.DownloadUrl.ShouldBe(new Uri($"https://storage.test/{path}"));
        _storage.Deletes.ShouldBeEmpty();
    }

    [Fact]
    public async Task DOC_09_A_failed_commit_removes_the_blob()
    {
        _unitOfWork.FailCommit = true;

        await Should.ThrowAsync<TimeoutException>(() => HandleAsync(Upload()));

        _storage.Deletes.ShouldBe([_storage.Uploads.ShouldHaveSingleItem().Path]);
    }

    [Fact]
    public async Task DOC_09_A_blob_whose_row_was_committed_after_all_is_kept()
    {
        _unitOfWork.FailCommit = true;
        _queries.DocumentExists = true; // the commit reached the database, but its acknowledgement did not reach us

        await Should.ThrowAsync<TimeoutException>(() => HandleAsync(Upload()));

        _storage.Uploads.ShouldHaveSingleItem();
        _storage.Deletes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public async Task TR_13_Nothing_is_stored_for_a_read_only_claim(ClaimStatus status)
    {
        _queries.Status = status;

        var violation = await Should.ThrowAsync<BusinessRuleViolationException>(() => HandleAsync(Upload()));

        violation.Errors["Claim"].ShouldBe([$"Claim is {status}; no changes are permitted."]);
        _storage.Uploads.ShouldBeEmpty();
    }

    [Fact]
    public async Task API_08_Nothing_is_stored_for_an_unknown_or_foreign_claim()
    {
        _queries.Status = null;

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Upload()));

        _storage.Uploads.ShouldBeEmpty();
    }

    [Fact]
    public async Task DOC_05_Nothing_is_stored_for_content_that_does_not_match_its_type()
    {
        var violation = await Should.ThrowAsync<BusinessRuleViolationException>(
            () => HandleAsync(Upload("invoice.pdf", content: "MZ\u0090\0 not a pdf")));

        violation.Errors["File"].ShouldBe(["The file content does not match its type."]);
        _storage.Uploads.ShouldBeEmpty();
    }

    private static UploadClaimDocumentCommand Upload(string fileName = "report.pdf", string content = "%PDF-1.7 minimal") =>
        new(Guid.Empty, new MemoryStream(Encoding.Latin1.GetBytes(content)), fileName, "application/pdf", null, null);

    private Task<DocumentDto> HandleAsync(UploadClaimDocumentCommand command) =>
        new UploadClaimDocumentCommandHandler(
                _queries,
                new FakeClaimRepository(_claim),
                _unitOfWork,
                _storage,
                new FakeTenant(),
                new FakeCurrentUser(),
                new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero)),
                NullLogger<UploadClaimDocumentCommandHandler>.Instance)
            .Handle(command with { ClaimId = _claim.Id }, CancellationToken.None);

    private static Claim NewClaim()
    {
        var now = new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
        var policy = Policy.Create("POL-TEST-000001", "Test Client Ltd", new DateOnly(2025, 1, 1), new DateOnly(2030, 12, 31), PolicyStatus.Active, ["Property"]);
        return Claim.Create(
            ClaimNumber.Create(2026, 1),
            policy,
            new LossEventDetails(now.AddDays(-1), "Burst pipe flooded the ground floor office.", null, "COL-WAT-PIP", null, null),
            ClaimSeverity.Standard,
            [new PartyDetails(PartyRole.Claimant, PartyType.Company, null, null, "Test Client Ltd", null, null, null)],
            [],
            Uploader,
            now);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool FailCommit { get; set; }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
        {
            var result = await operation(cancellationToken);
            return FailCommit ? throw new TimeoutException("Commit failed.") : result;
        }
    }

    private sealed class FakeStorage : IStorageService
    {
        public List<(string Path, string ContentType)> Uploads { get; } = [];

        public List<string> Deletes { get; } = [];

        public Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken)
        {
            Uploads.Add((objectPath, contentType));
            return Task.CompletedTask;
        }

        public Task<SignedDownloadUrl> GetDownloadUrlAsync(string objectPath, DownloadHeaders headers, TimeSpan validFor, CancellationToken cancellationToken) =>
            Task.FromResult(new SignedDownloadUrl(new Uri($"https://storage.test/{objectPath}"), DateTimeOffset.MaxValue));

        public Task DeleteAsync(string objectPath, CancellationToken cancellationToken)
        {
            Deletes.Add(objectPath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClaimRepository(Claim claim) : IClaimRepository
    {
        public Task<Claim?> GetAsync(Guid claimId, CancellationToken cancellationToken) => Task.FromResult<Claim?>(claimId == claim.Id ? claim : null);

        public void Add(Claim claim) => throw new NotSupportedException();
    }

    private sealed class FakeClaimQueries : IClaimQueries
    {
        public ClaimStatus? Status { get; set; }

        public bool DocumentExists { get; set; }

        public Task<ClaimStatus?> GetStatusAsync(Guid claimId, CancellationToken cancellationToken) => Task.FromResult(Status);

        public Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken) => Task.FromResult(DocumentExists);

        public Task<PagedResult<ClaimSummaryDto>> ListAsync(ClaimListFilter filter, PageRequest page, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClaimDetailDto?> GetDetailAsync(Guid claimId, int recentAuditEntries, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResult<AuditEntryDto>?> GetAuditAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ValidationIssueDto>?> ListValidationIssuesAsync(Guid claimId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClaimReservesDto?> GetReservesAsync(Guid claimId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ClaimDocumentRecord>?> ListDocumentsAsync(Guid claimId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClaimDocumentRecord?> GetDocumentAsync(Guid claimId, Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTenant : ITenantContext
    {
        public Guid? OrganisationId => UploadClaimDocumentCommandHandlerTests.OrganisationId;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => Uploader.UserId;

        public string? DisplayName => "Alex Carter";

        public UserRole? Role => Uploader.Role;

        public Guid? OrganisationId => UploadClaimDocumentCommandHandlerTests.OrganisationId;
    }
}
