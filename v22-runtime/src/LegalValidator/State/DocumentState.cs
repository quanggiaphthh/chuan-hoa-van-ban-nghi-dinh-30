using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Model;
namespace Nd30.LegalValidator.State;
public sealed record DocumentIdentity(string Algorithm,string Digest,string Scope="whole-document-bytes") {
 public const string SupportedAlgorithm="SHA-256";
 public static bool IsValid(DocumentIdentity? x)=>x is not null&&x.Algorithm==SupportedAlgorithm&&x.Scope=="whole-document-bytes"&&Regex.IsMatch(x.Digest,"^[0-9a-f]{64}$",RegexOptions.CultureInvariant);
}
public static class DocumentIdentityService {
 public static DocumentIdentity FromFile(string path,CancellationToken cancellationToken=default){try{cancellationToken.ThrowIfCancellationRequested();var info=new FileInfo(path);if(!info.Exists)throw new DocumentPackageException("MALFORMED_DOCX","The DOCX package could not be read.");if(info.Length>PackageSafetyLimits.MaximumCompressedInputBytes)throw new DocumentPackageException("INPUT_TOO_LARGE","The DOCX input exceeds the allowed size.");using var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.SequentialScan);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[64*1024];long total=0;while(true){cancellationToken.ThrowIfCancellationRequested();var read=s.Read(buffer,0,buffer.Length);if(read==0)break;total=checked(total+read);if(total>PackageSafetyLimits.MaximumCompressedInputBytes)throw new DocumentPackageException("INPUT_TOO_LARGE","The DOCX input exceeds the allowed size.");hash.AppendData(buffer,0,read);}return new(DocumentIdentity.SupportedAlgorithm,Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());}catch(DocumentPackageException){throw;}catch(OperationCanceledException){throw;}catch(Exception){throw new DocumentPackageException("MALFORMED_DOCX","The DOCX package could not be read.");}}
}
public sealed record RuleIdentity(string RuleId,string SourceId,string SourceLocator) {
 public static RuleIdentity From(ValidationResult r)=>new(r.RuleId,r.Source.SourceId,r.Source.Locator);
}
public sealed record DocumentStateBinding(DocumentIdentity ExpectedDocument,RuleIdentity ExpectedRule);
public enum StaleStateDecision { MATCH, STALE_DOCUMENT, MISSING_EXPECTED_IDENTITY, UNSUPPORTED_IDENTITY, RULE_IDENTITY_MISMATCH }
public sealed record StaleStateResult(StaleStateDecision Decision,bool StateMatches,bool Authorized,string Reason);
public static class StaleStateVerifier {
 public static StaleStateResult Verify(DocumentStateBinding? expected,DocumentIdentity actual,RuleIdentity actualRule){
  if(expected is null||expected.ExpectedDocument is null)return R(StaleStateDecision.MISSING_EXPECTED_IDENTITY,"Expected document identity is required.");
  if(!DocumentIdentity.IsValid(expected.ExpectedDocument)||!DocumentIdentity.IsValid(actual))return R(StaleStateDecision.UNSUPPORTED_IDENTITY,"Only canonical SHA-256 whole-document identities are supported.");
  if(expected.ExpectedDocument!=actual)return R(StaleStateDecision.STALE_DOCUMENT,"Document bytes no longer match proposal state.");
  if(expected.ExpectedRule!=actualRule)return R(StaleStateDecision.RULE_IDENTITY_MISMATCH,"Rule/source identity no longer matches proposal state.");
  return new(StaleStateDecision.MATCH,true,false,"State matches; this is not authorization.");
 }
 static StaleStateResult R(StaleStateDecision d,string reason)=>new(d,false,false,reason);
}
