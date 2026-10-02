#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
errors = []


def source(relative: str) -> str:
    path = ROOT / relative
    if not path.is_file():
        errors.append(f"MISSING:{relative}")
        return ""
    return path.read_text(encoding="utf-8")


limits = source("src/DocumentEngine/Safety/PackageSafetyLimits.cs")
for token in [
    "MaximumCompressedInputBytes",
    "MaximumTotalUncompressedBytes",
    "MaximumSingleEntryUncompressedBytes",
    "MaximumEntryCount",
    "MaximumEntryCompressionRatio",
    "MaximumXmlCharactersPerPart",
    "MaximumXmlDepth",
    "MaximumXmlNodesPerPart",
    "MaximumTotalXmlBytes",
]:
    if token not in limits:
        errors.append(f"LIMIT_MISSING:{token}")

preflight = source("src/DocumentEngine/Safety/PackageSafetyPreflight.cs")
for token in ["DtdProcessing.Prohibit", "XmlResolver = null", "ReadEntryCountBeforeArchive", "NormalizeAndValidateEntryName", "BoundedEntryReadStream", "MaximumTotalXmlBytes"]:
    if token not in preflight:
        errors.append(f"PREFLIGHT_MISSING:{token}")

parser = source("src/DocumentEngine/Parsing/DocxParser.cs")
if "WordprocessingDocument.Open(snapshot.StagedPath" not in parser:
    errors.append("PARSER_DOES_NOT_USE_VALIDATED_SNAPSHOT")

identity = source("src/LegalValidator/State/DocumentState.cs")
if "PackageSafetyLimits.MaximumCompressedInputBytes" not in identity or "IncrementalHash" not in identity:
    errors.append("DOCUMENT_IDENTITY_HASH_IS_NOT_BOUNDED")

alignment = source("src/LegalValidator/Mutation/ParagraphAlignmentMutationExecutor.cs")
for token in ["PackagePreserver.CopyWithoutMutation(source,tmp,cancellationToken)", "DocumentIdentityService.FromFile(tmp).Digest!=sh", "Resolve(tmp,i.Target)", "Other(tmp,i.Target.StructuralPath)"]:
    if token not in alignment:
        errors.append(f"ALIGNMENT_SNAPSHOT_MISSING:{token}")
if "Resolve(source,i.Target)" in alignment or "File.Copy(source,tmp,false)" in alignment:
    errors.append("ALIGNMENT_PARSER_CAN_REOPEN_UNVALIDATED_SOURCE")
else:
    ordered = [alignment.find("CopyWithoutMutation(source,tmp,cancellationToken)"), alignment.find("FromFile(tmp).Digest!=sh"), alignment.find("Resolve(tmp,i.Target)"), alignment.find("WordprocessingDocument.Open(tmp,true)")]
    if any(position < 0 for position in ordered) or ordered != sorted(ordered):
        errors.append("ALIGNMENT_SNAPSHOT_ORDER_INVALID")

atomic = source("src/LegalValidator/Execution/AtomicMutationPlanExecutor.cs")
for token in ["PackagePreserver.CopyWithoutMutation(source,tmp)", "DocumentIdentityService.FromFile(tmp).Digest!=input.Digest"]:
    if token not in atomic:
        errors.append(f"ATOMIC_SNAPSHOT_MISSING:{token}")
if "File.Copy(source,tmp,false)" in atomic:
    errors.append("ATOMIC_EXECUTOR_COPIES_UNPREFLIGHTED_SOURCE")
else:
    ordered = [atomic.find("CopyWithoutMutation(source,tmp)"), atomic.find("FromFile(tmp).Digest!=input.Digest"), atomic.find("WordprocessingDocument.Open(tmp,true)")]
    if any(position < 0 for position in ordered) or ordered != sorted(ordered):
        errors.append("ATOMIC_SNAPSHOT_ORDER_INVALID")

print(f"DOCUMENT SECURITY CORRECTIVE AUDIT: {'PASS' if not errors else 'FAIL'}")
for error in errors:
    print(error)
sys.exit(1 if errors else 0)
