#!/usr/bin/env python3
from pathlib import Path
p=Path(__file__).resolve().parent/'materialize_v25_l4c.py'
s=p.read_text(encoding='utf-8')
old='static string Doc(W.JustificationValues? a=W.JustificationValues.Left,bool prot=false){'
new='static string Doc(W.JustificationValues? a=null,bool prot=false,bool missingDirect=false){if(a is null&&!missingDirect)a=W.JustificationValues.Left;'
if old not in s: raise SystemExit('expected CS1736 source pattern not found')
s=s.replace(old,new,1)
old_call='var s=Doc(null);var o=s+".o";'
new_call='var s=Doc(null,missingDirect:true);var o=s+".o";'
if old_call not in s: raise SystemExit('expected missing-direct fixture call not found')
s=s.replace(old_call,new_call,1)
p.write_text(s,encoding='utf-8')
print('V25 L4C CS1736 test-generator corrective applied')
