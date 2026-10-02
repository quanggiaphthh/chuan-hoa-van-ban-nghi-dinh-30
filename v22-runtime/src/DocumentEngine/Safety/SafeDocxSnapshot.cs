using System.Security.Cryptography;
using Nd30.DocumentEngine.Model;

namespace Nd30.DocumentEngine.Safety;

/// <summary>A bounded, preflighted, service-owned input snapshot.</summary>
public sealed class SafeDocxSnapshot : IAsyncDisposable, IDisposable
{
    private readonly string _stagingDirectory;
    private readonly Func<string, bool> _cleanup;
    private bool _disposed;

    private SafeDocxSnapshot(string stagingDirectory, string stagedPath, PackageSafetyState safety, string sha256, Func<string, bool> cleanup)
    {
        _stagingDirectory = stagingDirectory;
        _cleanup = cleanup;
        StagedPath = stagedPath;
        Safety = safety;
        Sha256 = sha256;
    }

    internal string StagedPath { get; }
    public PackageSafetyState Safety { get; }
    public string Sha256 { get; }

    public static Task<SafeDocxSnapshot> CreateAsync(Stream input, CancellationToken cancellationToken = default) =>
        CreateAsync(input, cancellationToken, Path.GetTempPath(), PackageSafetyBudget.Default);

    internal static Task<SafeDocxSnapshot> CreateAsync(Stream input, CancellationToken cancellationToken, string stagingRoot) =>
        CreateAsync(input, cancellationToken, stagingRoot, PackageSafetyBudget.Default);

    internal static async Task<SafeDocxSnapshot> CreateAsync(Stream input, CancellationToken cancellationToken, string stagingRoot, PackageSafetyBudget budget, Func<string, bool>? cleanup = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead) throw new DocumentPackageException("MALFORMED_DOCX", "The DOCX input stream is not readable.");
        cleanup ??= TryDelete;

        string? directory = null;
        string? path = null;
        try
        {
            Directory.CreateDirectory(stagingRoot);
            directory = CreatePrivateDirectory(stagingRoot);
            path = Path.Combine(directory, "input.docx");
            long copied = 0;
            var buffer = new byte[64 * 1024];
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    copied = checked(copied + read);
                    if (copied > budget.MaximumCompressedInputBytes)
                        throw new DocumentPackageException("INPUT_TOO_LARGE", "The DOCX input exceeds the allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var safety = PackageSafetyPreflight.Inspect(path, budget, cancellationToken);
            if (!safety.IsReadable)
            {
                var failure = safety.Diagnostics.FirstOrDefault();
                throw new DocumentPackageException(failure?.Code ?? "MALFORMED_DOCX", failure?.Message ?? "The DOCX package is malformed.");
            }
            await using var digestStream = File.OpenRead(path);
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(digestStream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            return new SafeDocxSnapshot(directory, path, safety, digest, cleanup);
        }
        catch (OperationCanceledException)
        {
            if (!cleanup(directory)) throw new DocumentPackageException("TEMP_CLEANUP_FAILED", "Temporary DOCX data could not be removed.");
            throw new DocumentPackageException("PROCESSING_CANCELLED", "DOCX processing was cancelled.");
        }
        catch
        {
            if (!cleanup(directory)) throw new DocumentPackageException("TEMP_CLEANUP_FAILED", "Temporary DOCX data could not be removed.");
            throw;
        }
    }

    internal void EnsureUnchanged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var stream = File.OpenRead(StagedPath);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sha256), Convert.FromHexString(actual)))
            throw new DocumentPackageException("UNSAFE_ARCHIVE", "The staged DOCX snapshot changed during processing.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!_cleanup(_stagingDirectory))
            throw new DocumentPackageException("TEMP_CLEANUP_FAILED", "Temporary DOCX data could not be removed.");
        _disposed = true;
    }

    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }

    private static string CreatePrivateDirectory(string stagingRoot)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var path = Path.Combine(stagingRoot, "docx-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(path);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                return path;
            }
            catch (IOException) when (Directory.Exists(path)) { }
        }
        throw new DocumentPackageException("TEMP_CREATE_FAILED", "A private processing workspace could not be created.");
    }

    private static bool TryDelete(string? directory)
    {
        if (directory is null) return true;
        try { Directory.Delete(directory, recursive: true); return true; }
        catch (DirectoryNotFoundException) { return true; }
        catch { return false; }
    }
}

public sealed class DocumentPackageException : Exception
{
    public DocumentPackageException(string code, string safeMessage) : base(safeMessage)
    {
        Code = code;
        SafeMessage = safeMessage;
    }

    public string Code { get; }
    public string SafeMessage { get; }
}
