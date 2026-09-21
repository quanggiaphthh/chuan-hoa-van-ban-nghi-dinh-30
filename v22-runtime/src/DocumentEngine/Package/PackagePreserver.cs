using System.IO.Compression; using System.Security.Cryptography;
namespace Nd30.DocumentEngine.Package;
public static class PackagePreserver {public static void CopyWithoutMutation(string input,string output){File.Copy(input,output,true);}public static string Sha256(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();}public static IReadOnlyList<string> PartInventory(string path){using var z=ZipFile.OpenRead(path);return z.Entries.Select(e=>e.FullName).OrderBy(x=>x,StringComparer.Ordinal).ToList();}}
