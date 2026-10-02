#!/usr/bin/env python3
"""Retain the thin host and test visibility after V24/V25 generation."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def apply_to_files(solution: Path, project: Path) -> None:
    source = solution.read_text(encoding="utf-8")
    line = '  <Project Path="src/DocumentProcessor.Host/DocumentProcessor.Host.csproj" />\n'
    if line not in source:
        seam = '  <Project Path="src/DocumentEngine/DocumentEngine.csproj" />\n'
        if source.count(seam) != 1:
            raise SystemExit("DocumentEngine solution seam is missing or ambiguous")
        solution.write_text(source.replace(seam, seam + line), encoding="utf-8")

    source = project.read_text(encoding="utf-8")
    visibility = '  <ItemGroup>\n    <InternalsVisibleTo Include="LegalValidator.Tests" />\n  </ItemGroup>\n'
    if visibility not in source:
        if source.count('</Project>') != 1:
            raise SystemExit("LegalValidator project seam is missing or ambiguous")
        project.write_text(source.replace('</Project>', visibility + '</Project>'), encoding="utf-8")


if __name__ == "__main__":
    apply_to_files(ROOT / "DocumentEngine.slnx", ROOT / "src/LegalValidator/LegalValidator.csproj")
    print("Document processor host materialization corrective applied")
