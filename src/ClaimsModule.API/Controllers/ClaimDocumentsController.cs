using ClaimsModule.API.Auth;
using ClaimsModule.API.Contracts.Claims;
using ClaimsModule.API.Storage;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Commands.UploadClaimDocument;
using ClaimsModule.Application.Claims.Queries.GetDocumentDownloadUrl;
using ClaimsModule.Application.Claims.Queries.ListClaimDocuments;
using ClaimsModule.Domain.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

/// <summary>Documents of a claim (FRS §10.1, §13, D-08).</summary>
[ApiController]
[Route("api/claims/{claimId:guid}/documents")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ClaimDocumentsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// The 50 MB file (FRS §13) plus room for the multipart envelope and the other fields. Kestrel's default body limit is
    /// about 28.6 MB (D-28), so this endpoint raises it. A file between 50 MB and this limit gets the validator's 422; a larger
    /// body gets 413 before it is read.
    /// </summary>
    private const long MaxRequestBytes = ClaimDocument.MaxFileSizeBytes + (1024 * 1024);

    /// <summary>Multipart upload: <c>file</c>, optional <c>documentType</c> (default Other) and <c>notes</c>. 201 with the document and a download URL.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RejectBodiesLargerThan(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DocumentDto>> Upload(Guid claimId, [FromForm] UploadDocumentForm form, CancellationToken cancellationToken)
    {
        // The form reader has buffered the file, so this stream is seekable: the handler sniffs it, then uploads it.
        await using var content = form.File?.OpenReadStream();
        var uploaded = await sender.Send(
            new UploadClaimDocumentCommand(claimId, content, form.File?.FileName, form.File?.ContentType, form.DocumentType, form.Notes),
            cancellationToken);

        // No Location header: a document is read through the list or its URL endpoint, not as a resource of its own.
        return StatusCode(StatusCodes.Status201Created, uploaded);
    }

    /// <summary>Every document, newest first, each with a download URL valid for one hour (BR-D-02).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(Guid claimId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListClaimDocumentsQuery(claimId), cancellationToken));

    /// <summary>A fresh one-hour download URL (D-08), for when the one from the list has expired.</summary>
    [HttpGet("{documentId:guid}/url")]
    [ProducesResponseType<DocumentDownloadUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDownloadUrlDto>> GetDownloadUrl(Guid claimId, Guid documentId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetDocumentDownloadUrlQuery(claimId, documentId), cancellationToken));
}
