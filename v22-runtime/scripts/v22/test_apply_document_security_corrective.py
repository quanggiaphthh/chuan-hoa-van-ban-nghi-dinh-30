import tempfile
import unittest
from pathlib import Path

from apply_document_security_corrective import (
    harden_alignment_snapshot_boundary,
    harden_atomic_snapshot_boundary,
    replace_once,
)


class DocumentSecurityCorrectiveTests(unittest.TestCase):
    def test_source_seam_is_applied_once_and_repeatable(self):
        with tempfile.TemporaryDirectory() as directory:
            source_path = Path(directory) / "generated.cs"
            source_path.write_text("before exact seam after", encoding="utf-8")

            replace_once(source_path, "exact seam", "bounded seam")
            first_result = source_path.read_text(encoding="utf-8")
            replace_once(source_path, "exact seam", "bounded seam")

            self.assertEqual("before bounded seam after", first_result)
            self.assertEqual(first_result, source_path.read_text(encoding="utf-8"))

    def test_source_seam_fails_closed_when_missing_or_ambiguous(self):
        with tempfile.TemporaryDirectory() as directory:
            source_path = Path(directory) / "generated.cs"
            source_path.write_text("seam seam", encoding="utf-8")

            with self.assertRaises(SystemExit):
                replace_once(source_path, "seam", "corrected")

            source_path.write_text("unrecognized source", encoding="utf-8")
            with self.assertRaises(SystemExit):
                replace_once(source_path, "seam", "corrected")

    def test_alignment_executor_parses_only_the_bounded_snapshot(self):
        source = (
            'if(File.Exists(dst))return Fail(MutationOutcome.OUTPUT_ALREADY_EXISTS,"Output exists.",q,input);'
            'var rr=Resolve(source,i.Target);'
            'var unrelated=Other(source,i.Target.StructuralPath);'
            'var parts=PackagePreserver.PartSha256Inventory(source,cancellationToken).Where(x=>!x.Key.Equals("word/document.xml",StringComparison.OrdinalIgnoreCase)).ToArray();'
            'var dir=Path.GetDirectoryName(dst)!;Directory.CreateDirectory(dir);'
            'tmp=Path.Combine(dir,$".{Path.GetFileName(dst)}.{Guid.NewGuid():N}.tmp");File.Copy(source,tmp,false);'
            'using(var d=WordprocessingDocument.Open(tmp,true))'
        )

        result = harden_alignment_snapshot_boundary(source)

        self.assertIn('PackagePreserver.CopyWithoutMutation(source,tmp,cancellationToken)', result)
        self.assertIn('DocumentIdentityService.FromFile(tmp).Digest!=sh', result)
        self.assertIn('Resolve(tmp,i.Target)', result)
        self.assertIn('Other(tmp,i.Target.StructuralPath)', result)
        self.assertNotIn('File.Copy(source,tmp,false)', result)
        self.assertEqual(result, harden_alignment_snapshot_boundary(result))

    def test_atomic_executor_validates_copy_before_open_xml(self):
        source = 'File.Copy(source,tmp,false);using(var d=WordprocessingDocument.Open(tmp,true))'

        result = harden_atomic_snapshot_boundary(source)

        self.assertIn('PackagePreserver.CopyWithoutMutation(source,tmp)', result)
        self.assertLess(result.index('Digest!=input.Digest'), result.index('WordprocessingDocument.Open'))
        self.assertNotIn('File.Copy(source,tmp,false)', result)
        self.assertEqual(result, harden_atomic_snapshot_boundary(result))

if __name__ == "__main__":
    unittest.main()
