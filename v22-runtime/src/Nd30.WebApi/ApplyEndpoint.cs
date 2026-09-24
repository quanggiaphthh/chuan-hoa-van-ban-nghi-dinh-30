using System.Security.Cryptography;
using Nd30.DocumentEngine;
using Nd30.SemanticDetector;
using Nd30.LegalValidator;
using Nd30.LegalValidator.Rules;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Execution;

internal static class ApplyEndpoint
{
    internal static async Task<IResult> Handle(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data required" });
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        var expectedDigest = form["documentDigest"].ToString();
        var selected = form["selectedProposalIds"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var idempotencyKey = form["idempotencyKey"].ToString();
        if (file is null || file.Length == 0 || string.IsNullOrWhiteSpace(expectedDigest) || selected.Length == 0 || string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest(new { error = "file, documentDigest, selectedProposalIds and idempotencyKey are required" });

        var source = Path.Combine(Path.GetTempPath(), $"nd30-{Guid.NewGuid():N}.docx");
        var output = Path.Combine(Path.GetTempPath(), $"nd30-{Guid.NewGuid():N}-output.docx");
        try
        {
            await using (var fs = File.Create(source)) await file.CopyToAsync(fs, ct);
            var digest = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source, ct))).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(digest), Convert.FromHexString(expectedDigest)))
                return Results.Conflict(new { error = "document digest mismatch" });

            var parsed = new DocxParser().Parse(source);
            var semantic = new AdministrativeSemanticDetector().Detect(parsed);
            var context = new SemanticValidationContextBuilder().Build(parsed, semantic);
            var catalog = RuleCatalog.LoadVerified();
            var validation = new ValidationEngine(catalog).Validate(context);
            var proposals = new RemediationPlanner().Plan(validation);
            var chosen = proposals.Where(p => selected.Contains(p.Id, StringComparer.Ordinal)).ToArray();
            if (chosen.Length != selected.Length) return Results.BadRequest(new { error = "unknown or stale proposal" });
            if (chosen.Any(p => p.Eligibility != AutofixEligibility.SafeAutofix)) return Results.BadRequest(new { error = "proposal is not safe-autofix eligible" });

            var intents = chosen.Select(p => MutationIntent.FromProposal(p)).ToArray();
            var identity = DocumentIdentity.FromFile(source);
            var artifact = AuthorizationArtifact.ForDocument(identity, intents);
            var authorized = new AuthorizationBoundary().Authorize(identity, artifact, intents);
            var plan = new MutationPlanFactory().Create(authorized, output, idempotencyKey);
            var result = new AtomicMutationPlanExecutor(new CanonicalDocumentRevalidator(catalog)).Execute(plan);
            if (!result.Succeeded || !File.Exists(output)) return Results.UnprocessableEntity(new { error = "canonical mutation/revalidation rejected" });
            return Results.File(output, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "normalized.docx", enableRangeProcessing: false);
        }
        catch (FormatException) { return Results.BadRequest(new { error = "invalid digest" }); }
        finally
        {
            try { if (File.Exists(source)) File.Delete(source); } catch { }
            // Results.File streams after handler return, so output cleanup is intentionally delegated to OS temp lifecycle.
        }
    }
}
