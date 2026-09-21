#!/usr/bin/env python3
from pathlib import Path
import re,sys,zipfile,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]; errors=[]
def req(p):
 q=root/p
 if not q.exists(): errors.append(f'MISSING:{p}')
 return q
files=['src/DocumentEngine/Model/Models.cs','src/DocumentEngine/Parsing/DocxParser.cs','src/DocumentEngine/Formatting/EffectiveFormattingResolver.cs','src/DocumentEngine/Safety/PackageSafetyPreflight.cs','src/DocumentEngine/Package/PackagePreserver.cs','tests/DocumentEngine.Tests/DocumentEngineTests.cs','scripts/v22/generate_docx_fixtures.py']
for f in files:req(f)
model=req('src/DocumentEngine/Model/Models.cs').read_text()
if 'DocumentFormat.OpenXml' in model: errors.append('CANONICAL_MODEL_EXPOSES_OPENXML')
for token in ['StyleCatalogModel','DocumentDefaultsModel','StyleModel','NumberingLevelModel','HeaderFooterReferenceModel','FieldType']:
 if token not in model:errors.append('MODEL_MISSING:'+token)
resolver=req('src/DocumentEngine/Formatting/EffectiveFormattingResolver.cs').read_text()
for token in ['Resolve(DocumentModel','document-default','inherited-style:','paragraph-style','run-style','direct-formatting','STYLE_INHERITANCE_CYCLE']:
 if token not in resolver:errors.append('RESOLVER_MISSING:'+token)
parser=req('src/DocumentEngine/Parsing/DocxParser.cs').read_text()
for token in ['FieldCharValues.Begin','FieldCharValues.Separate','FieldCharValues.End','SimpleField','FieldType.Page','FieldType.NumPages','HeaderReference','FooterReference','AbstractNum','NumberingLevelReference','OnOff(W.OnOffType']:
 if token not in parser:errors.append('PARSER_MISSING:'+token)
safety=req('src/DocumentEngine/Safety/PackageSafetyPreflight.cs').read_text()
for token in ['SIGNED_PACKAGE','MACRO_PRESENT','OLE_PRESENT','TRACKED_CHANGES_PRESENT','EXTERNAL_RELATIONSHIP','PatchPolicy.AUDIT_ONLY','PatchPolicy.PROHIBITED']:
 if token not in safety:errors.append('SAFETY_MISSING:'+token)
prod='\n'.join(p.read_text(errors='ignore') for p in (root/'src').rglob('*') if p.is_file())
if 'python-docx' in prod or 'import docx' in prod:errors.append('PYTHON_DOCX_PRODUCTION_DEPENDENCY')
fx=list((root/'fixtures/docx').glob('*.doc*'))
if len(fx)<14:errors.append(f'FIXTURE_COUNT:{len(fx)}')
for name in ['15-signed-structure-simulation.docx','16-readonly-parts.docx','17-numbering.docx','18-onoff-semantics.docx','19-style-cycle.docx']:
 if not (root/'fixtures/docx'/name).exists():errors.append('FIXTURE_MISSING:'+name)
NSR='{http://schemas.openxmlformats.org/package/2006/relationships}'; NSC='{http://schemas.openxmlformats.org/package/2006/content-types}'
for f in fx:
 try:
  with zipfile.ZipFile(f) as z:
   names=set(z.namelist()); needed={'[Content_Types].xml','_rels/.rels','word/document.xml'}
   if not needed<=names:errors.append(f'OPC_REQUIRED:{f.name}:{needed-names}');continue
   ct=ET.fromstring(z.read('[Content_Types].xml')); overrides={x.attrib['PartName'].lstrip('/'):x.attrib['ContentType'] for x in ct.findall(NSC+'Override')}
   main=overrides.get('word/document.xml','')
   if f.suffix=='.docm':
    if main!='application/vnd.ms-word.document.macroEnabled.main+xml':errors.append('DOCM_MAIN_TYPE:'+f.name)
   elif main!='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml':errors.append('DOCX_MAIN_TYPE:'+f.name)
   rr=ET.fromstring(z.read('_rels/.rels')); office=[x for x in rr.findall(NSR+'Relationship') if x.attrib.get('Type','').endswith('/officeDocument')]
   if len(office)!=1 or office[0].attrib.get('Target')!='word/document.xml':errors.append('ROOT_REL:'+f.name)
   for part in names:
    if part.startswith('word/header') and part.endswith('.xml') and 'header+xml' not in overrides.get(part,''):errors.append('HEADER_TYPE:'+f.name+':'+part)
    if part.startswith('word/footer') and part.endswith('.xml') and 'footer+xml' not in overrides.get(part,''):errors.append('FOOTER_TYPE:'+f.name+':'+part)
   if 'word/_rels/document.xml.rels' in names:
    rel=ET.fromstring(z.read('word/_rels/document.xml.rels'))
    for x in rel.findall(NSR+'Relationship'):
     if x.attrib.get('TargetMode')=='External':continue
     target=x.attrib.get('Target',''); resolved='word/'+target.lstrip('/') if not target.startswith('/') else target.lstrip('/')
     if resolved not in names:errors.append(f'BROKEN_REL:{f.name}:{target}')
 except Exception as e:errors.append(f'OPC_PARSE:{f.name}:{e}')
print(f'V22 STATIC AUDIT: {"PASS" if not errors else "FAIL"}');print(f'fixtures={len(fx)} errors={len(errors)}')
for e in errors:print(e)
T=(root/'tests/DocumentEngine.Tests/DocumentEngineTests.cs').read_text(); P=(root/'src/DocumentEngine/Parsing/DocxParser.cs').read_text(); M=(root/'src/DocumentEngine/Model/Models.cs').read_text(); G=(root/'scripts/v22/generate_docx_fixtures.py').read_text(); S=(root/'src/DocumentEngine/Safety/PackageSafetyPreflight.cs').read_text()
for token in ['OpenXmlValidator','Validate(d)','20-line-spacing.docx','21-negative-margin.docx','22-unsupported-altchunk.docx']:
 if token not in T: errors.append('FINAL_TEST_MISSING:'+token)
for token in ['LineSpacingModel','LineSpacingRule','RawValue','Lines','Points']:
 if token not in M: errors.append('LINE_SPACING_MODEL_MISSING:'+token)
if 'LineSpacingPt' in M or 'LineSpacingPt' in P: errors.append('LEGACY_LINE_SPACING_PT_PRESENT')
if 'Math.Max(0' in P: errors.append('MARGIN_NORMALIZATION_PRESENT')
for token in ['m?.Top?.Value','m?.Bottom?.Value','Relationships(main)','m.Parts','ExternalRelationships']:
 if token not in P: errors.append('PARSER_FINAL_MISSING:'+token)
for token in ['altChunk','activeX','attachedTemplate','embedded-package','linked-ole']:
 if token not in S: errors.append('UNSUPPORTED_DETECTION_MISSING:'+token)
if not (root/'fixtures/docx/22-unsupported-altchunk.docx').exists():errors.append('UNSUPPORTED_FIXTURE_MISSING')
hf=root/'fixtures/docx/08-header-footer.docx'
if hf.exists():
 with zipfile.ZipFile(hf) as z:
  doc=ET.fromstring(z.read('word/document.xml')); settings=ET.fromstring(z.read('word/settings.xml'))
  if any(x.tag.endswith('evenAndOddHeaders') for x in doc.iter()):errors.append('EVEN_ODD_WRONG_IN_DOCUMENT')
  if not any(x.tag.endswith('evenAndOddHeaders') for x in settings.iter()):errors.append('EVEN_ODD_SETTING_MISSING')
print(f"V22 FINAL STATIC GATE: {'PASS' if not errors else 'FAIL'}")
for e in errors: print(e)
sys.exit(1 if errors else 0)
