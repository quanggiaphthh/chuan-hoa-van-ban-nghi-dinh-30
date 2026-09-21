#!/usr/bin/env python3
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'v22-runtime/src/LegalValidator/Authorization/AuthorizationBoundary.cs'
s=p.read_text()
needle='namespace Nd30.LegalValidator.Authorization;'
first=s.find(needle); second=s.find(needle,first+len(needle))
if second<0: raise SystemExit('second namespace not found')
s=s[:second]+s[second+len(needle):]
p.write_text(s)
print('V26 final authorization namespace corrective applied')
