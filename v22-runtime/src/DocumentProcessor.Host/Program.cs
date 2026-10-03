using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Processing;

const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
var maximumBodyBytes = PackageSafetyLimits.MaximumCompressedInputBytes;
var sharedToken = Environment.GetEnvironmentVariable("DOCUMENT_PROCESSOR_SHARED_TOKEN");
if (string.IsNullOrWhiteSpace(sharedToken) || Encoding.UTF8.GetByteCount(sharedToken) < 32)
    throw new InvalidOperationException("DOCUMENT_PROCESSOR_SHARED_TOKEN must be configured with at least 32 bytes.");

var builder = WebApplication.CreateBuilder(args);
// Local verification defaults to loopback. Production ingress and service IAM
// are deployment controls; the bearer token remains defense in depth.
var configuredUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(configuredUrls))
    builder.WebHost.UseUrls("http://127.0.0.1:5080");
else if (builder.Environment.IsDevelopment() && configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var url) ||
                  !url.IsLoopback || url.Scheme != Uri.UriSchemeHttp))
    throw new InvalidOperationException("Development processor URLs must bind loopback HTTP only.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumBodyBytes);
// The verified release catalog and the company formatting profile are resolved
// once at startup so the serving flow runs the real rule engine. A missing pack
// is a startup failure, never a silently degraded formatter.
var ruleCatalog = RuleCatalog.LoadVerifiedRelease(ResolveLegalRoot());
builder.Services.AddSingleton(ruleCatalog);
builder.Services.AddSingleton<DocumentProcessorService>();
builder.Services.AddSingleton(new SemaphoreSlim(1, 1));

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (!HasValidBearer(context.Request.Headers["Authorization"].ToString(), sharedToken))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Authentication required.", code = "UNAUTHORIZED" }, context.RequestAborted);
        return;
    }
    await next(context);
});
app.Use(async (context, next) =>
{
    var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = maximumBodyBytes;
    if (context.Request.ContentLength is > 0 && context.Request.ContentLength > maximumBodyBytes)
    {
        await WriteError(context, StatusCodes.Status413PayloadTooLarge, "INPUT_TOO_LARGE", "The DOCX input exceeds the allowed size.", CorrelationId(context));
        return;
    }
    await next(context);
});

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapGet("/capabilities", (DocumentProcessorService processor) => Results.Json(new
{
    profile = DocumentProcessorService.Profile,
    profileId = processor.CurrentProfile?.Id,
    profileVersion = processor.CurrentProfile?.Version,
    profileDigest = processor.CurrentProfile?.Digest,
    rulePack = processor.CurrentProfile?.RulePackId,
    ruleSubsetSize = processor.CurrentProfile?.BaseRuleIds.Count,
    documentTypes = processor.CurrentProfile?.DocumentTypes.Select(type => new
    {
        key = type.TypeKey,
        labelVi = type.LabelVi,
        hasTypeHeading = type.HasTypeHeading,
    }),
    supportedProperties = processor.CurrentProfile?.MutationTargets.Select(target => new
    {
        property = target.Property,
        scope = target.Scope,
        unit = target.Unit,
        labelVi = target.LabelVi,
        ruleId = target.RuleId,
    }),
    maximumInputBytes = PackageSafetyLimits.MaximumCompressedInputBytes,
    maximumOutputBytes = DocumentProcessorService.MaximumOutputBytes,
    // Advertise exactly what this build can do: inspect plus the profile-driven apply.
    // `paragraph.alignment.direct` was the pre-profile spike name and is no longer a
    // real operation; apply is the single `/apply` route bound to a profile property,
    // target, profile digest and rule id. Stored execution records keep replaying under
    // their original logical id, so this metadata change breaks no persisted state.
    operations = new[] { "inspect", "apply" },
}));

app.MapPost("/inspect", async (HttpContext context) =>
{
    var correlationId = CorrelationId(context);
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    if (!HasDocxContentType(context.Request.ContentType))
        return await WriteError(context, StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_FILE_TYPE", "A DOCX binary request is required.", correlationId);
    var gate = context.RequestServices.GetRequiredService<SemaphoreSlim>();
    if (!await gate.WaitAsync(0, context.RequestAborted))
        return await WriteError(context, StatusCodes.Status429TooManyRequests, "PROCESSOR_BUSY", "The document processor is busy.", correlationId);
    try
    {
        var processor = context.RequestServices.GetRequiredService<DocumentProcessorService>();
        // An optional owner-confirmed document type re-scopes the whole inspection:
        // the rule subset, findings and mutable targets are rebuilt for that type.
        var confirmedTypeKey = context.Request.Query["confirmedDocumentTypeKey"].ToString();
        var result = await processor.InspectAsync(
            context.Request.Body,
            string.IsNullOrWhiteSpace(confirmedTypeKey) ? null : confirmedTypeKey,
            context.RequestAborted);
        return Results.Json(result);
    }
    catch (DocumentProcessorException error)
    {
        return await WriteProcessorError(context, error, correlationId);
    }
    catch (OperationCanceledException)
    {
        return await WriteError(context, 499, "PROCESSING_CANCELLED", "DOCX processing was cancelled.", correlationId);
    }
    catch (Exception)
    {
        return await WriteError(context, StatusCodes.Status500InternalServerError, "PROCESSING_FAILED", "DOCX inspection failed.", correlationId);
    }
    finally { gate.Release(); }
});

app.MapPost("/apply", async (HttpContext context) =>
{
    var correlationId = CorrelationId(context);
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    if (!HasDocxContentType(context.Request.ContentType))
        return await WriteError(context, StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_FILE_TYPE", "A DOCX binary request is required.", correlationId);
    var query = context.Request.Query;
    var sourceSha256 = query["sourceSha256"].ToString();
    var paragraphId = query["paragraphId"].ToString();
    var expectedBefore = query["expectedBefore"].ToString();
    var desiredAfter = query["desiredAfter"].ToString();
    var profileDigest = query["profileDigest"].ToString();
    var profileId = query["profileId"].ToString();
    var ruleId = query["ruleId"].ToString();
    // Profile-driven formatting apply. `property` selects the profile target;
    // `targetId` names the paragraph or section it belongs to.
    var property = query["property"].ToString();
    var targetId = query["targetId"].ToString();
    var documentTypeKey = query["documentTypeKey"].ToString();
    var isProfileApply = property.Length > 0;

    // The inspection binding is mandatory on the profile path: an apply that cannot
    // name the profile and the exact current value it was confirmed against is
    // refused before any work starts.
    if (sourceSha256.Length != 64 || sourceSha256.Any(character => !Uri.IsHexDigit(character)) ||
        (isProfileApply
            ? (profileDigest.Length != 64 || profileDigest.Any(character => !Uri.IsHexDigit(character))
                || documentTypeKey.Length is 0 or > 32
                || expectedBefore.Length is 0 or > 64
                || property.Length is 0 or > 128
                || targetId.Length is 0 or > 128
                || desiredAfter.Length is 0 or > 64)
            : (paragraphId.Length is < 2 or > 10 || paragraphId[0] != 'p' || !int.TryParse(paragraphId.AsSpan(1), out _)
                || expectedBefore.Length == 0
                || profileDigest.Length > 0 && (profileDigest.Length != 64 || profileDigest.Any(character => !Uri.IsHexDigit(character)))
                || desiredAfter.Length is 0 or > 64)))
        return await WriteError(context, StatusCodes.Status400BadRequest, "INVALID_INPUT", "The confirmed document operation is invalid.", correlationId);

    var gate = context.RequestServices.GetRequiredService<SemaphoreSlim>();
    if (!await gate.WaitAsync(0, context.RequestAborted))
        return await WriteError(context, StatusCodes.Status429TooManyRequests, "PROCESSOR_BUSY", "The document processor is busy.", correlationId);
    try
    {
        var processor = context.RequestServices.GetRequiredService<DocumentProcessorService>();
        if (isProfileApply)
        {
            var formatted = await processor.ApplyFormattingAsync(context.Request.Body, new FormattingApplyRequest(
                sourceSha256.ToLowerInvariant(), documentTypeKey, property, targetId, expectedBefore, desiredAfter,
                profileId.Length > 0 ? profileId : null,
                profileDigest.Length > 0 ? profileDigest.ToLowerInvariant() : null,
                ruleId.Length > 0 ? ruleId : null), context.RequestAborted);

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = DocxMime;
            context.Response.ContentLength = formatted.OutputBytes.Length;
            context.Response.Headers["Content-Disposition"] = "attachment; filename=\"formatted.docx\"";
            context.Response.Headers["X-Output-SHA256"] = formatted.OutputSha256;
            context.Response.Headers["X-Document-Reopened"] = "true";
            context.Response.Headers["X-Document-Revalidated"] = "true";
            context.Response.Headers["X-Applied-Property"] = formatted.Property;
            context.Response.Headers["X-Applied-Target"] = formatted.TargetId;
            context.Response.Headers["X-Applied-Rule-Id"] = formatted.RuleId;
            context.Response.Headers["X-Applied-Document-Type"] = formatted.DocumentTypeKey;
            if (formatted.Binding.Digest.Length > 0) context.Response.Headers["X-Profile-Digest"] = formatted.Binding.Digest;
            if (formatted.Binding.Version.Length > 0) context.Response.Headers["X-Profile-Version"] = formatted.Binding.Version;
            await context.Response.Body.WriteAsync(formatted.OutputBytes, context.RequestAborted);
            return Results.Empty;
        }

        if (!new[] { "LEFT", "CENTER", "RIGHT", "JUSTIFY" }.Contains(expectedBefore, StringComparer.Ordinal) ||
            !new[] { "LEFT", "CENTER", "RIGHT", "JUSTIFY" }.Contains(desiredAfter, StringComparer.Ordinal))
            return await WriteError(context, StatusCodes.Status400BadRequest, "INVALID_INPUT", "The confirmed alignment request is invalid.", correlationId);

        var result = await processor.ApplyAlignmentAsync(context.Request.Body, sourceSha256, paragraphId,
            expectedBefore, desiredAfter, context.RequestAborted,
            profileDigest.Length > 0 ? profileDigest : null,
            profileId.Length > 0 ? profileId : null,
            ruleId.Length > 0 ? ruleId : null);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = DocxMime;
        context.Response.ContentLength = result.OutputBytes.Length;
        context.Response.Headers["Content-Disposition"] = "attachment; filename=\"formatted.docx\"";
        context.Response.Headers["X-Output-SHA256"] = result.OutputSha256;
        context.Response.Headers["X-Document-Reopened"] = "true";
        context.Response.Headers["X-Document-Revalidated"] = "true";
        context.Response.Headers["X-Applied-Rule-Id"] = result.RuleId;
        if (result.ProfileDigest.Length > 0) context.Response.Headers["X-Profile-Digest"] = result.ProfileDigest;
        if (result.ProfileVersion.Length > 0) context.Response.Headers["X-Profile-Version"] = result.ProfileVersion;
        await context.Response.Body.WriteAsync(result.OutputBytes, context.RequestAborted);
        return Results.Empty;
    }
    catch (DocumentProcessorException error)
    {
        return await WriteProcessorError(context, error, correlationId);
    }
    catch (OperationCanceledException)
    {
        return await WriteError(context, 499, "PROCESSING_CANCELLED", "DOCX processing was cancelled.", correlationId);
    }
    catch (Exception)
    {
        return await WriteError(context, StatusCodes.Status500InternalServerError, "PROCESSING_FAILED", "DOCX alignment could not be applied.", correlationId);
    }
    finally { gate.Release(); }
});

await app.RunAsync();

static bool HasValidBearer(string authorization, string expectedToken)
{
    const string prefix = "Bearer ";
    if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
    var candidate = Encoding.UTF8.GetBytes(authorization[prefix.Length..].Trim());
    var expected = Encoding.UTF8.GetBytes(expectedToken);
    return CryptographicOperations.FixedTimeEquals(SHA256.HashData(candidate), SHA256.HashData(expected));
}

/// <summary>
/// Locate the verified release pack. An explicit override wins; otherwise walk
/// up from the binary until the pack is found, which covers both a source run and
/// a published layout.
/// </summary>
static string ResolveLegalRoot()
{
    var configured = Environment.GetEnvironmentVariable("DOCUMENT_PROCESSOR_LEGAL_ROOT");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        if (File.Exists(Path.Combine(configured, "release", "admin-nd30-verified-rc-v20.yaml"))) return configured;
        throw new InvalidOperationException("DOCUMENT_PROCESSOR_LEGAL_ROOT does not contain the verified release pack.");
    }
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "release", "admin-nd30-verified-rc-v20.yaml"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Verified release pack not found from the processor base directory.");
}

static bool HasDocxContentType(string? contentType) =>
    string.Equals(contentType?.Split(';', 2)[0].Trim(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase);

static string CorrelationId(HttpContext context)
{
    var value = context.Request.Headers["X-Correlation-ID"].ToString();
    return value.Length is > 0 and <= 96 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
        ? value
        : Guid.NewGuid().ToString("N");
}

static async Task<IResult> WriteProcessorError(HttpContext context, DocumentProcessorException error, string correlationId)
{
    var status = error.Code switch
    {
        "INPUT_TOO_LARGE" or "OUTPUT_TOO_LARGE" => StatusCodes.Status413PayloadTooLarge,
        "PROCESSING_TIMEOUT" => StatusCodes.Status504GatewayTimeout,
        "PROCESSING_CANCELLED" => 499,
        "STALE_DOCUMENT" or "PRECONDITION_FAILED" => StatusCodes.Status409Conflict,
        "AUTHORIZATION_REJECTED" => StatusCodes.Status403Forbidden,
        "UNSUPPORTED_FILE_TYPE" => StatusCodes.Status415UnsupportedMediaType,
        "PROCESSOR_BUSY" or "PROCESSING_BUSY" => StatusCodes.Status429TooManyRequests,
        "TEMP_CLEANUP_FAILED" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
    return await WriteError(context, status, error.Code, error.SafeMessage, correlationId);
}

static async Task<IResult> WriteError(HttpContext context, int status, string code, string message, string correlationId)
{
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { error = message, code, correlationId }, context.RequestAborted);
    return Results.Empty;
}
