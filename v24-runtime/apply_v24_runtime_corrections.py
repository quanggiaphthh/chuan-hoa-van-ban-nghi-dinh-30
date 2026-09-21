#!/usr/bin/env python3
from pathlib import Path

TEST_FILE = Path('v22-runtime/tests/LegalValidator.Tests/LegalValidatorTests.cs')

if not TEST_FILE.is_file():
    raise SystemExit(f'expected materialized V24 test file missing: {TEST_FILE}')

text = TEST_FILE.read_text(encoding='utf-8')
if '[Fact]' not in text:
    raise SystemExit('expected xUnit [Fact] tests not found in materialized LegalValidatorTests.cs')

if 'using Xunit;' not in text:
    text = 'using Xunit;\n' + text
    TEST_FILE.write_text(text, encoding='utf-8')
    print('v24 runtime correction applied: added missing using Xunit;')
else:
    print('v24 runtime correction already present: using Xunit;')
