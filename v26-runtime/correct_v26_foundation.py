#!/usr/bin/env python3
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'v22-runtime/src/LegalValidator/Execution/V26ExecutionSafety.cs'
s=p.read_text(encoding='utf-8')
s=s.replace('public sealed class FileExecutionJournal{','public sealed class FileExecutionJournal:IDisposable{')
s=s.replace('if(g.Count()>semantic.Count)classes.Add(ConflictClass.DUPLICATE);if(semantic.Select(x=>x.DesiredAfter)', 'var duplicate=g.Count()>semantic.Count;if(duplicate)classes.Add(ConflictClass.DUPLICATE);if(semantic.Select(x=>x.DesiredAfter)')
s=s.replace('classes.Add(ConflictClass.INDEPENDENT);ops.Add(semantic[0]);}', 'if(!duplicate)classes.Add(ConflictClass.INDEPENDENT);ops.Add(semantic[0]);}')
s=s.replace('\n void Save(List<ExecutionRecord> a){', '\n public void Dispose(){}\n void Save(List<ExecutionRecord> a){')
p.write_text(s,encoding='utf-8')
print('V26 confirmed generator corrective applied')
