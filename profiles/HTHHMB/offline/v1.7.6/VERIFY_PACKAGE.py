"""Portable offline verification. Usage: python VERIFY_PACKAGE.py <bundle.zip>
Python stdlib only. Validates packaged workbook/CSVs against original v1.7.5.
It intentionally does NOT claim legal or rendered DOCX QA.
"""
import zipfile,io,sys,re,csv,json,hashlib,xml.etree.ElementTree as ET
N={'x':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def shared(z):
 if 'xl/sharedStrings.xml' not in z.namelist():return []
 rt=ET.fromstring(z.read('xl/sharedStrings.xml'))
 return [''.join(t.text or '' for t in el.findall('.//x:t',N)) for el in rt.findall('x:si',N)]
def sheet_values(z,i,strings):
 root=ET.fromstring(z.read(f'xl/worksheets/sheet{i}.xml'))
 result={}
 for c in root.findall('.//x:sheetData/x:row/x:c',N):
  v=c.find('x:v',N);f=c.find('x:f',N);inline=c.find('x:is',N)
  x=v.text if v is not None else None
  if c.get('t')=='s' and x is not None:x=strings[int(x)]
  if c.get('t')=='inlineStr' and inline is not None:x=''.join(t.text or '' for t in inline.findall('.//x:t',N))
  result[c.get('r')]=(x,f.text if f is not None else None)
 return result
def fcount(z):
 return sum(len(ET.fromstring(z.read(fn)).findall('.//x:sheetData/x:row/x:c/x:f',N)) for fn in z.namelist() if re.fullmatch(r'xl/worksheets/sheet\d+\.xml',fn))
def main(path):
 with zipfile.ZipFile(path) as bundle:
  assert bundle.testzip() is None,'ZIP CRC error'
  files=bundle.namelist()
  mpath='RULE_HTHHMB_v1.7.6/manifest.json'
  m=json.loads(bundle.read(mpath))
  op=m['output_workbook']
  data=bundle.read(op)
  assert hashlib.sha256(data).hexdigest()==m['output_sha256'],'Workbook SHA mismatch'
  old='RULE_HTHHMB_v1.7.6/ORIGINALS/'+m['source_workbook']
  legacy=bundle.read(old)
  assert hashlib.sha256(legacy).hexdigest()==m['source_sha256'],'Source SHA mismatch'
  with zipfile.ZipFile(io.BytesIO(data)) as wb, zipfile.ZipFile(io.BytesIO(legacy)) as base:
   assert wb.testzip() is None and base.testzip() is None
   newcount=len([x for x in wb.namelist() if re.fullmatch(r'xl/worksheets/sheet\d+\.xml',x)])
   assert newcount==41
   a=shared(base);b=shared(wb)
   for i in range(1,27):
    assert sheet_values(base,i,a)==sheet_values(wb,i,b),f'Legacy sheet {i} changed'
   assert fcount(base)==fcount(wb)==4372,'Formula count'
  def readcsv(rel):return list(csv.DictReader(io.StringIO(bundle.read('RULE_HTHHMB_v1.7.6/'+rel).decode('utf-8'))))
  parents=readcsv('MA_TRAN_165.csv');tables=readcsv('AUDIT_BANG_132.csv');rules=readcsv('RULE_ALL_ATTRIBUTES.csv')
  assert len(parents)==165 and len({x['Ma_cha'] for x in parents})==165
  assert len(tables)==132 and len({x['Mã bảng'] for x in tables})==132
  assert len(rules)==868 and len({x['Mã rule con'] for x in rules})==868
  assert len([x for x in files if re.search(r'/rules/RULE_D_(ND30|HD05)\.md$',x)])==2
  print(json.dumps({'result':'PASS','sheets':newcount,'source_sheets_unchanged':26,'formulas':4372,'parent_rules':165,'table_rules':132,'new_CNGX':868,'scope':'STRUCTURAL_ONLY'},ensure_ascii=False))
if __name__=='__main__':
 if len(sys.argv)!=2:raise SystemExit('Usage: python VERIFY_PACKAGE.py path/to/bundle.zip')
 main(sys.argv[1])
