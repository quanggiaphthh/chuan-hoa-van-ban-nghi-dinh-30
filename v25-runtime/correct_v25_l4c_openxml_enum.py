#!/usr/bin/env python3
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'v22-runtime/src/LegalValidator/Mutation/ParagraphAlignmentMutationExecutor.cs'
s=p.read_text(encoding='utf-8')
old='return v switch{W.JustificationValues.Left=>ParagraphAlignmentValue.LEFT,W.JustificationValues.Center=>ParagraphAlignmentValue.CENTER,W.JustificationValues.Right=>ParagraphAlignmentValue.RIGHT,W.JustificationValues.Both=>ParagraphAlignmentValue.JUSTIFY,_=>null};'
new='if(v==W.JustificationValues.Left)return ParagraphAlignmentValue.LEFT;if(v==W.JustificationValues.Center)return ParagraphAlignmentValue.CENTER;if(v==W.JustificationValues.Right)return ParagraphAlignmentValue.RIGHT;if(v==W.JustificationValues.Both)return ParagraphAlignmentValue.JUSTIFY;return null;'
if old not in s: raise SystemExit('expected OpenXML v3 switch pattern not found')
p.write_text(s.replace(old,new),encoding='utf-8')
print('V25 L4C OpenXML enum corrective applied')