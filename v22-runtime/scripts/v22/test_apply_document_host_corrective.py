import tempfile
import unittest
from pathlib import Path

from apply_document_host_corrective import apply_to_files


class HostMaterializationTests(unittest.TestCase):
    def test_host_and_test_visibility_survive_regeneration_idempotently(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            solution = root / "DocumentEngine.slnx"
            project = root / "LegalValidator.csproj"
            solution.write_text('<Solution>\n  <Project Path="src/DocumentEngine/DocumentEngine.csproj" />\n</Solution>\n')
            project.write_text('<Project Sdk="Microsoft.NET.Sdk">\n</Project>\n')
            apply_to_files(solution, project)
            first = solution.read_text(), project.read_text()
            apply_to_files(solution, project)
            self.assertEqual(first, (solution.read_text(), project.read_text()))
            self.assertIn('DocumentProcessor.Host.csproj', first[0])
            self.assertIn('InternalsVisibleTo Include="LegalValidator.Tests"', first[1])


if __name__ == "__main__":
    unittest.main()
