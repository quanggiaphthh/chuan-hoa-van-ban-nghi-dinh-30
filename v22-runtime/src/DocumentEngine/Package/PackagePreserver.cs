using System.IO.Compression;
using System.Security.Cryptography;
using Nd30.DocumentEngine.Safety;

namespace Nd30.DocumentEngine.Package;

public static class PackagePreserver
{
    /// <summary>Copy-only helper. Existing output files and source-overwrite attempts are rejected.</summary>
    public static void CopyWithoutMutation(string input, string output, CancellationToken cancellationToken = default)
    {
        var originalSource = Path.GetFullPath(input);
        var destination = Path.GetFullPath(output);
        if (StringComparer.OrdinalIgnoreCase.Equals(originalSource, destination))
            throw new DocumentPackageException("OUTPUT_PATH_INVALID", "The output must be a new DOCX artifact.");
        if (File.Exists(destination))
            throw new DocumentPackageException("OUTPUT_ALREADY_EXISTS", "The output artifact already exists.");
        using var inputStream = File.OpenRead(input);
        using var snapshot = SafeDocxSnapshot.CreateAsync(inputStream, cancellationToken).GetAwaiter().GetResult();
        var created = false;
        try
        {
            snapshot.EnsureUnchanged();
            using (var staged = new FileStream(snapshot.StagedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
            using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
            {
                created = true;
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = staged.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    target.Write(buffer, 0, read);
                }
                target.Flush(flushToDisk: true);
            }
            snapshot.EnsureUnchanged();
            if (new FileInfo(destination).Length > PackageSafetyLimits.MaximumCompressedInputBytes)
                throw new DocumentPackageException("OUTPUT_TOO_LARGE", "The output artifact exceeds the allowed size.");
        }
        catch
        {
            if (created)
            {
                try { File.Delete(destination); }
                catch { throw new DocumentPackageException("TEMP_CLEANUP_FAILED", "A temporary DOCX artifact could not be removed."); }
            }
            throw;
        }
    }

    public static string Sha256(string path, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        if (stream.Length > PackageSafetyLimits.MaximumCompressedInputBytes)
            throw new DocumentPackageException("INPUT_TOO_LARGE", "The DOCX input exceeds the allowed size.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            total = checked(total + read);
            if (total > PackageSafetyLimits.MaximumCompressedInputBytes)
                throw new DocumentPackageException("INPUT_TOO_LARGE", "The DOCX input exceeds the allowed size.");
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static IReadOnlyList<string> PartInventory(string path)
    {
        using var input = File.OpenRead(path);
        using var snapshot = SafeDocxSnapshot.CreateAsync(input).GetAwaiter().GetResult();
        using var archive = ZipFile.OpenRead(snapshot.StagedPath);
        return archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Hashes each bounded part without materializing its expanded bytes in memory.</summary>
    public static IReadOnlyDictionary<string, string> PartSha256Inventory(string path, CancellationToken cancellationToken = default)
    {
        using var input = File.OpenRead(path);
        SafeDocxSnapshot snapshot;
        try { snapshot = SafeDocxSnapshot.CreateAsync(input, cancellationToken).GetAwaiter().GetResult(); }
        catch (DocumentPackageException error) when (error.Code == "PROCESSING_CANCELLED" && cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        using (snapshot)
        {
            using var archive = ZipFile.OpenRead(snapshot.StagedPath);
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var content = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = content.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    hash.AppendData(buffer, 0, read);
                }
                hashes.Add(entry.FullName, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
            }
            return hashes;
        }
    }
}
