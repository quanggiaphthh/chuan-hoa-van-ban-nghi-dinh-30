using Nd30.DocumentEngine.Parsing;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
using Nd30.SemanticDetector.Detection;

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
        try
        {
            await using (var fs = File.Create(source)) await file.CopyToAsync(fs, ct);
            var identity = DocumentIdentityService.FromFile(source);
            if (!string.Equals(identity.Digest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new { error = "document digest mismatch" });

            var parsed = new DocxParser().Parse(source);
            if (parsed.Document is null) return Results.BadRequest(new { error = "DOCX could not be parsed." });
            var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document);
            var context = SemanticValidationContextBuilder.Build(parsed.Document, semantic, DateOnly.FromDateTime(DateTime.UtcNow));
            var catalog = RuleCatalog.LoadVerifiedRelease(FindCanonicalRoot());
            var report = new ValidationEngine(catalog).Validate(context);
            var planner = new RemediationPlanner();

            foreach (var result in report.Results)
            {
                var rule = RuleIdentity.From(result);
                var proposal = planner.Propose(result, parsed.Document.Safety, "format", new DocumentStateBinding(identity, rule));
                if (!selected.Contains(proposal.ProposalId, StringComparer.Ordinal)) continue;
                if (!proposal.Executable) return Results.BadRequest(new { error = "proposal is not executable" });
                return Results.BadRequest(new { error = "selected proposal requires canonical target/value materialization not exposed by validation result" });
            }

            return Results.BadRequest(new { error = "unknown or stale proposal" });
        }
        finally { try { if (File.Exists(source)) File.Delete(source); } catch { } }
    }

    private static string FindCanonicalRoot()
    {
        var configured = Environment.GetEnvironmentVariable("ND30_CANONICAL_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "release", "admin-nd30-verified-rc-v20.yaml")))
            return configured;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "release", "admin-nd30-verified-rc-v20.yaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Canonical ND30 release root not found.");
    }
}
