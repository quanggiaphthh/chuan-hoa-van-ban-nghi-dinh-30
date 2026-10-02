#!/usr/bin/env python3
"""Apply the post-materialization DOCX security/fidelity corrections to V25/V26 source."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def replace_once(path: Path, old: str, new: str) -> None:
    source = path.read_text(encoding="utf-8")
    if new in source:
        return
    count = source.count(old)
    if count != 1:
        try:
            display_path = path.relative_to(ROOT)
        except ValueError:
            display_path = path.name
        raise SystemExit(f"Expected one source seam in {display_path}; found {count}.")
    path.write_text(source.replace(old, new, 1), encoding="utf-8")


def replace_text_once(source: str, old: str, new: str, seam: str) -> str:
    if new in source:
        return source
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"Expected one {seam} source seam; found {count}.")
    return source.replace(old, new, 1)


def harden_alignment_snapshot_boundary(source: str) -> str:
    new = (
        'var unrelated=Other(tmp,i.Target.StructuralPath);'
        'var parts=PackagePreserver.PartSha256Inventory(tmp,cancellationToken).Where(x=>!x.Key.Equals("word/document.xml",StringComparison.OrdinalIgnoreCase)).ToArray();'
        'using(var d=WordprocessingDocument.Open(tmp,true))'
    )
    if new in source:
        return source
    insert_before = 'if(File.Exists(dst))return Fail(MutationOutcome.OUTPUT_ALREADY_EXISTS,"Output exists.",q,input);var rr=Resolve(source,i.Target);'
    inserted = 'if(File.Exists(dst))return Fail(MutationOutcome.OUTPUT_ALREADY_EXISTS,"Output exists.",q,input);var dir=Path.GetDirectoryName(dst)!;Directory.CreateDirectory(dir);tmp=Path.Combine(dir,$".{Path.GetFileName(dst)}.{Guid.NewGuid():N}.tmp");PackagePreserver.CopyWithoutMutation(source,tmp,cancellationToken);if(DocumentIdentityService.FromFile(tmp).Digest!=sh)return CF(MutationOutcome.STALE_DOCUMENT,"Input snapshot changed after preflight.",q,input,tmp);var rr=Resolve(tmp,i.Target);'
    if inserted not in source:
        source = replace_text_once(source, insert_before, inserted, "alignment snapshot creation")
    old_variants = [
        'var unrelated=Other(source,i.Target.StructuralPath);var parts=PackagePreserver.PartInventory(source);var dir=Path.GetDirectoryName(dst)!;Directory.CreateDirectory(dir);tmp=Path.Combine(dir,$".{Path.GetFileName(dst)}.{Guid.NewGuid():N}.tmp");File.Copy(source,tmp,false);using(var d=WordprocessingDocument.Open(tmp,true))',
        'var unrelated=Other(source,i.Target.StructuralPath);var parts=PackagePreserver.PartSha256Inventory(source,cancellationToken).Where(x=>!x.Key.Equals("word/document.xml",StringComparison.OrdinalIgnoreCase)).ToArray();var dir=Path.GetDirectoryName(dst)!;Directory.CreateDirectory(dir);tmp=Path.Combine(dir,$".{Path.GetFileName(dst)}.{Guid.NewGuid():N}.tmp");File.Copy(source,tmp,false);using(var d=WordprocessingDocument.Open(tmp,true))',
    ]
    for old in old_variants:
        if old in source:
            return replace_text_once(source, old, new, "alignment parser boundary")
    raise SystemExit("Expected one alignment parser boundary source seam; found 0.")


def harden_atomic_snapshot_boundary(source: str) -> str:
    return replace_text_once(
        source,
        'File.Copy(source,tmp,false);using(var d=WordprocessingDocument.Open(tmp,true))',
        'PackagePreserver.CopyWithoutMutation(source,tmp);if(DocumentIdentityService.FromFile(tmp).Digest!=input.Digest)throw new IntegrityException("Input snapshot changed after preflight.");using(var d=WordprocessingDocument.Open(tmp,true))',
        "atomic mutation parser boundary",
    )


def apply() -> None:
    mutation = ROOT / "src/LegalValidator/Mutation/ParagraphAlignmentMutationExecutor.cs"
    if not mutation.exists():
        raise SystemExit("V25 alignment source was not materialized before security corrective.")

    replace_once(
        mutation,
        "SOURCE_IMMUTABILITY_VIOLATION}",
        "SOURCE_IMMUTABILITY_VIOLATION,OUTPUT_TOO_LARGE,PROCESSING_CANCELLED}",
    )
    replace_once(
        mutation,
        "Execute(string source,string output,AuthorizedMutationRequest q){",
        "Execute(string source,string output,AuthorizedMutationRequest q,CancellationToken cancellationToken=default){",
    )
    replace_once(
        mutation,
        "var sh=input.Digest;string? tmp=null;try{var i=q.Intent;",
        "var sh=input.Digest;string? tmp=null;try{cancellationToken.ThrowIfCancellationRequested();var i=q.Intent;",
    )
    replace_once(mutation, "PackageSafetyPreflight.Inspect(source);", "PackageSafetyPreflight.Inspect(source,cancellationToken);")
    replace_once(
        mutation,
        "var safe=PackageSafetyPreflight.Inspect(source,cancellationToken);if(!safe.IsReadable",
        "var safe=PackageSafetyPreflight.Inspect(source,cancellationToken);cancellationToken.ThrowIfCancellationRequested();if(!safe.IsReadable",
    )
    replace_once(
        mutation,
        "d.MainDocumentPart!.Document.Save();}var ps=PackageSafetyPreflight.Inspect(tmp);",
        "d.MainDocumentPart!.Document.Save();}cancellationToken.ThrowIfCancellationRequested();if(new FileInfo(tmp).Length>PackageSafetyLimits.MaximumCompressedInputBytes)return CF(MutationOutcome.OUTPUT_TOO_LARGE,\"Output exceeds the allowed size.\",q,input,tmp);var ps=PackageSafetyPreflight.Inspect(tmp,cancellationToken);cancellationToken.ThrowIfCancellationRequested();",
    )
    replace_once(
        mutation,
        "if(!parts.SequenceEqual(PackagePreserver.PartInventory(tmp))||Other(tmp,i.Target.StructuralPath)!=unrelated)",
        "if(!parts.SequenceEqual(PackagePreserver.PartSha256Inventory(tmp,cancellationToken).Where(x=>!x.Key.Equals(\"word/document.xml\",StringComparison.OrdinalIgnoreCase)))||Other(tmp,i.Target.StructuralPath)!=unrelated)",
    )
    replace_once(
        mutation,
        "var oid=DocumentIdentityService.FromFile(tmp);if(oid.Digest==sh)",
        "cancellationToken.ThrowIfCancellationRequested();var oid=DocumentIdentityService.FromFile(tmp);if(oid.Digest==sh)",
    )
    replace_once(
        mutation,
        "}catch(Exception e){if(tmp is not null)Del(tmp);return Fail(DocumentIdentityService.FromFile(source).Digest==sh?MutationOutcome.WRITE_FAILED:MutationOutcome.SOURCE_IMMUTABILITY_VIOLATION,e.Message,q,input);}",
        "}catch(OperationCanceledException){return Fail(MutationOutcome.PROCESSING_CANCELLED,\"DOCX processing was cancelled.\",q,input);}catch(Exception){if(tmp is not null)Del(tmp);return Fail(MutationOutcome.WRITE_FAILED,\"DOCX processing failed safely.\",q,input);}",
    )
    mutation.write_text(harden_alignment_snapshot_boundary(mutation.read_text(encoding="utf-8")), encoding="utf-8")

    atomic = ROOT / "src/LegalValidator/Execution/AtomicMutationPlanExecutor.cs"
    if atomic.exists():
        atomic.write_text(harden_atomic_snapshot_boundary(atomic.read_text(encoding="utf-8")), encoding="utf-8")
        atomic_source = atomic.read_text(encoding="utf-8")
        atomic_source = replace_text_once(
            atomic_source,
            'catch(Exception e){journal.Fail(eid,"WRITE_FAILED");Del(tmp);tmp=null;return F(PlanExecutionOutcome.WRITE_FAILED,e.Message,eid,input);}',
            'catch(Exception){journal.Fail(eid,"WRITE_FAILED");Del(tmp);tmp=null;return F(PlanExecutionOutcome.WRITE_FAILED,"DOCX processing failed safely.",eid,input);}',
            "atomic safe diagnostic",
        )
        atomic.write_text(atomic_source, encoding="utf-8")

    revalidator = ROOT / "src/LegalValidator/Execution/CanonicalDocumentRevalidator.cs"
    if revalidator.exists():
        replace_once(
            revalidator,
            "string.Join(\"; \",parsed.Diagnostics.Select(x=>x.Message))",
            "\"The document could not be parsed during revalidation.\"",
        )
        replace_once(
            revalidator,
            "catch(Exception e){return new(false,false,false,[],[],[],e.Message);}",
            "catch(Exception){return new(false,false,false,[],[],[],\"Canonical document revalidation failed.\");}",
        )

    print("DOCX security corrective applied to materialized production source")


if __name__ == "__main__":
    apply()
