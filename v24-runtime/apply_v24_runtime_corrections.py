#!/usr/bin/env python3
from pathlib import Path

TEST_FILE = Path('v22-runtime/tests/LegalValidator.Tests/LegalValidatorTests.cs')

if not TEST_FILE.is_file():
    raise SystemExit(f'expected materialized V24 test file missing: {TEST_FILE}')

text = TEST_FILE.read_text(encoding='utf-8')
if '[Fact]' not in text:
    raise SystemExit('expected xUnit [Fact] tests not found in materialized LegalValidatorTests.cs')

changes = []
if 'using Xunit;' not in text:
    text = 'using Xunit;\n' + text
    changes.append('added missing using Xunit;')

wrong_fixture = 'fixtures","v23","01-named-decision.docx'
correct_fixture = 'fixtures","v23","02-named-decision.docx'
if wrong_fixture in text:
    text = text.replace(wrong_fixture, correct_fixture)
    changes.append('corrected named-decision fixture path to 02-named-decision.docx')

TEST_FILE.write_text(text, encoding='utf-8')
if changes:
    print('v24 runtime corrections applied: ' + '; '.join(changes))
else:
    print('v24 runtime corrections already present')
