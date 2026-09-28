using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims.Documents;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Claims.Commands.UploadClaimDocument;

/// <summary>
/// POST /api/claims/{id}/documents (FRS §10.1, §13): a multipart upload. <see cref="Content"/> must be seekable (the API
/// passes the buffered form file). <see cref="DocumentType"/> defaults to Other, because the FRS §11.3 upload button is
/// only a file picker (D-42). Audit: DOCUMENT_UPLOADED with RelatedEntityId = documentId.
/// </summary>
public sealed record UploadClaimDocumentCommand(
    Guid ClaimId,
    Stream? Content,
    string? FileName,
    string? DeclaredContentType,
    DocumentType? DocumentType,
    string? Notes) : ICommand<DocumentDto>, IHandlesOwnUnitOfWork;

/// <summary>Request shape only. The type allowlist, the content sniffing and the claim's state are checked by the handler and the domain.</summary>
internal sealed class UploadClaimDocumentCommandValidator : AbstractValidator<UploadClaimDocumentCommand>
{
    public const string FileRequiredMessage = "A file is required.";

    public UploadClaimDocumentCommandValidator()
    {
        RuleFor(command => command.Content)
            .NotNull().WithMessage(FileRequiredMessage).OverridePropertyName(ErrorKeys.File)
            .DependentRules(() =>
            {
                RuleFor(command => command.Content!.Length)
                    .GreaterThan(0).WithMessage(ClaimDocument.FileEmptyMessage)
                    .LessThanOrEqualTo(ClaimDocument.MaxFileSizeBytes).WithMessage(ClaimDocument.FileTooLargeMessage)
                    .OverridePropertyName(ErrorKeys.File);

                RuleFor(command => command.FileName)
                    .MaximumLength(FieldLengths.Name).WithMessage(RequestMessages.TooLong("File name", FieldLengths.Name))
                    .OverridePropertyName(ErrorKeys.File);
            });

        RuleFor(command => command.DocumentType)
            .IsInEnum().WithMessage(ClaimDocument.InvalidDocumentTypeMessage)
            .When(command => command.DocumentType is not null);

        RuleFor(command => command.Notes)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Notes", FieldLengths.Reason));
    }
}

/// <summary>
/// Blob first, metadata second (D-42). Network I/O never runs inside the database transaction, and an execution-strategy
/// replay of the transaction never uploads twice (the request stream can be read once, the document id is fixed before the upload).
/// <list type="number">
/// <item>Sanitise the name, resolve the format from the allowlist, sniff the content: nothing is stored for a bad file.</item>
/// <item>Refuse early when the claim is missing, in another organisation or read-only (the aggregate re-checks at step 4).</item>
/// <item>Upload to <c>{org}/{claim}/{documentId}_{name}</c>.</item>
/// <item>One unit of work: load the claim, <see cref="Claim.AddDocument"/>, audit DOCUMENT_UPLOADED, commit.</item>
/// <item>If step 4 fails, delete the blob, unless the row exists after all (a commit whose outcome was unknown): a missing
/// blob would lose data, an orphan blob only wastes space.</item>
/// </list>
/// </summary>
internal sealed class UploadClaimDocumentCommandHandler(
    IClaimQueries claimQueries,
    IClaimRepository claims,
    IUnitOfWork unitOfWork,
    IStorageService storage,
    ITenantContext tenant,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<UploadClaimDocumentCommandHandler> logger) : IRequestHandler<UploadClaimDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        var content = request.Content ?? throw new InvalidOperationException("The validator requires a file.");
        var fileName = SanitisedFileName.From(request.FileName);
        var format = DocumentFormat.Resolve(fileName, request.DeclaredContentType);
        if (!DocumentContentInspector.Matches(format, content))
        {
            throw new BusinessRuleViolationException(ErrorKeys.File, DocumentContentInspector.ContentMismatchMessage);
        }

        var status = await claimQueries.GetStatusAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);
        Claim.EnsureModifiable(status);

        var organisationId = tenant.OrganisationId ?? throw new InvalidOperationException("The request has no organisation.");
        var blobPath = DocumentBlobPath.For(organisationId, request.ClaimId, SequentialGuid.NewGuid(), fileName);
        var uploader = currentUser.ToActor();

        await storage.UploadAsync(blobPath.Value, content, format.ContentType, cancellationToken);

        ClaimDocument document;
        try
        {
            document = await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    var claim = await claims.GetAsync(request.ClaimId, token)
                        ?? throw new NotFoundException(nameof(Claim), request.ClaimId);
                    return claim.AddDocument(
                        blobPath, request.DocumentType ?? DocumentType.Other, content.Length, request.Notes, uploader, timeProvider.GetUtcNow());
                },
                cancellationToken);
        }
        catch
        {
            await RemoveUncommittedBlobAsync(blobPath);
            throw;
        }

        return await storage.ToDtoAsync(
            new ClaimDocumentRecord(
                document.Id,
                document.DocumentType,
                document.DocumentName,
                document.ContentType,
                document.FileSizeBytes,
                document.UploadedAt,
                document.UploadedByUserId,
                currentUser.DisplayName,
                document.Notes,
                document.BlobPath),
            cancellationToken);
    }

    private async Task RemoveUncommittedBlobAsync(DocumentBlobPath blobPath)
    {
        // Not the request's token: a client that disconnects mid-commit must not leave the blob behind either.
        try
        {
            if (await claimQueries.DocumentExistsAsync(blobPath.DocumentId, CancellationToken.None))
            {
                logger.LogWarning("Document {DocumentId} was committed although the unit of work failed; its blob is kept", blobPath.DocumentId);
                return;
            }

            await storage.DeleteAsync(blobPath.Value, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove the blob of uncommitted document {DocumentId}; it is an orphan", blobPath.DocumentId);
        }
    }
}
