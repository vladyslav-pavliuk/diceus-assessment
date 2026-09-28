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

[ApiController]
[Route("api/claims/{claimId:guid}/documents")]
[Authorize(Policy = AuthorizationPolicies.Handler)]
public sealed class ClaimDocumentsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// 50 MB plus room for the multipart envelope; Kestrel's default is about 28.6 MB (D-28). A file just over 50 MB gets the
    /// validator's 422, a larger body a 413 before it is read.
    /// </summary>
    private const long MaxRequestBytes = ClaimDocument.MaxFileSizeBytes + (1024 * 1024);

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
        // The form reader buffered the file, so the stream is seekable for content sniffing.
        await using var content = form.File?.OpenReadStream();
        var uploaded = await sender.Send(
            new UploadClaimDocumentCommand(claimId, content, form.File?.FileName, form.File?.ContentType, form.DocumentType, form.Notes),
            cancellationToken);

        // No Location header: a document is read through the list or its URL endpoint, not as a resource of its own.
        return StatusCode(StatusCodes.Status201Created, uploaded);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(Guid claimId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListClaimDocumentsQuery(claimId), cancellationToken));

    [HttpGet("{documentId:guid}/url")]
    [ProducesResponseType<DocumentDownloadUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDownloadUrlDto>> GetDownloadUrl(Guid claimId, Guid documentId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetDocumentDownloadUrlQuery(claimId, documentId), cancellationToken));
}
