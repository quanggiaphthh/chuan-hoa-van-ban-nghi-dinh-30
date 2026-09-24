using Nd30.DocumentEngine.Parsing;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
using Nd30.SemanticDetector.Detection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);
var app = builder.Build();

app.MapPost("/api/v1/analyze", async (IFormFile file, CancellationToken cancellationToken) =>
{
    if (file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "A non-empty .docx file is required." });

    var work = Path.Combine(Path.GetTempPath(), "nd30-web", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(work);
    var input = Path.Combine(work, "input.docx");

    try
    {
        await using (var stream = File.Create(input))
            await file.CopyToAsync(stream, cancellationToken);

        var parsed = new DocxParser().Parse(input);
        if (parsed.Document is null)
            return Results.BadRequest(new { error = "DOCX could not be parsed.", diagnostics = parsed.Diagnostics });

        var identity = DocumentIdentityService.FromFile(input);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document);
        var context = SemanticValidationContextBuilder.Build(parsed.Document, semantic, DateOnly.FromDateTime(DateTime.UtcNow));
        var catalog = RuleCatalog.LoadVerifiedRelease(FindCanonicalRoot());
        var report = new ValidationEngine(catalog).Validate(context);
        var planner = new RemediationPlanner();

        var findings = report.Results.Select(result =>
        {
            var proposal = planner.Propose(
                result,
                parsed.Document.Safety,
                operation: "format",
                stateBinding: new DocumentStateBinding(identity, RuleIdentity.From(result)));

            return new
            {
                id = proposal.FindingReference,
                ruleId = result.RuleId,
                status = result.Status.ToString(),
                severity = result.Severity,
                component = result.Target.Key,
                message = result.Reason,
                currentValue = result.Observed,
                proposedValue = result.Expected,
                patchEligibility = result.PatchEligibility.ToString(),
                proposal = new
                {
                    id = proposal.ProposalId,
                    decision = proposal.Decision.ToString(),
                    executable = proposal.Executable,
                    reason = proposal.Reason,
                    targetId = proposal.TargetId
                }
            };
        }).ToArray();

        return Results.Ok(new
        {
            documentDigest = identity.Digest,
            findings,
            capabilities = new Dictionary<string, string>
            {
                ["paragraph.alignment"] = "SAFE_AUTOFIX",
                ["paragraph.spacing.after"] = "SAFE_AUTOFIX",
                ["line.spacing"] = "DETECT_ONLY",
                ["indentation"] = "DETECT_ONLY",
                ["font.family"] = "DETECT_ONLY",
                ["font.size"] = "DETECT_ONLY",
                ["bold"] = "DETECT_ONLY",
                ["italic"] = "DETECT_ONLY",
                ["page.margins"] = "DETECT_ONLY",
                ["section.properties"] = "DETECT_ONLY"
            },
            summary = new
            {
                report.PassCount,
                report.FailCount,
                report.NeedsReviewCount,
                report.NotEvaluatedCount,
                report.NotApplicableCount,
                report.EvaluatedCoveragePercent,
                report.FullComplianceClaimAllowed,
                report.ComplianceStatement
            }
        });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
    catch (Exception)
    {
        return Results.Problem("Canonical analysis failed.", statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    finally
    {
        try { Directory.Delete(work, recursive: true); } catch { }
    }
}).DisableAntiforgery();

app.Run();

static string FindCanonicalRoot()
{
    var configured = Environment.GetEnvironmentVariable("ND30_CANONICAL_ROOT");
    if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "release", "admin-nd30-verified-rc-v20.yaml")))
        return configured;

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "release", "admin-nd30-verified-rc-v20.yaml")))
            return directory.FullName;
        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Canonical ND30 release root not found.");
}

public partial class Program { }
