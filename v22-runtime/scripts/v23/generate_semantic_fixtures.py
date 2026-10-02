#!/usr/bin/env python3
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
from html import escape
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "fixtures" / "v23"
OUT.mkdir(parents=True, exist_ok=True)
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
R='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
PR='http://schemas.openxmlformats.org/package/2006/relationships'
CT='http://schemas.openxmlformats.org/package/2006/content-types'
ZIP_TIME=(1980,1,1,0,0,0)

def add_entry(archive,name,data):
    info=ZipInfo(name,ZIP_TIME);info.compress_type=ZIP_DEFLATED;info.create_system=3;info.external_attr=0o600<<16
    archive.writestr(info,data)


def p(text, bold=False, italic=False, align=None, before=None, after=None):
    rpr=''
    if bold or italic:
        rpr='<w:rPr>' + ('<w:b/>' if bold else '') + ('<w:i/>' if italic else '') + '</w:rPr>'
    ppr=[]
    if align: ppr.append(f'<w:jc w:val="{align}"/>')
    if before is not None or after is not None:
        attrs=[]
        if before is not None: attrs.append(f'w:before="{before}"')
        if after is not None: attrs.append(f'w:after="{after}"')
        ppr.append('<w:spacing ' + ' '.join(attrs) + '/>')
    pp='<w:pPr>'+''.join(ppr)+'</w:pPr>' if ppr else ''
    return f'<w:p>{pp}<w:r>{rpr}<w:t xml:space="preserve">{escape(text)}</w:t></w:r></w:p>'


def table(cells):
    grid=''.join('<w:gridCol w:w="4500"/>' for _ in cells)
    row=''.join('<w:tc><w:tcPr/>'+''.join(p(x) for x in cell)+'</w:tc>' for cell in cells)
    return f'<w:tbl><w:tblPr/><w:tblGrid>{grid}</w:tblGrid><w:tr>{row}</w:tr></w:tbl>'


def make(name, body):
    document=f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="{W}" xmlns:r="{R}"><w:body>{body}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701"/></w:sectPr></w:body></w:document>'''
    styles=f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="{W}"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Times New Roman"/><w:sz w:val="28"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:jc w:val="both"/></w:pPr></w:pPrDefault></w:docDefaults><w:style w:type="paragraph" w:styleId="Normal"><w:name w:val="Normal"/></w:style></w:styles>'''
    settings=f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:settings xmlns:w="{W}"/>'''
    rels=f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PR}"><Relationship Id="rStyles" Type="{R}/styles" Target="styles.xml"/><Relationship Id="rSettings" Type="{R}/settings" Target="settings.xml"/></Relationships>'''
    rootrels=f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PR}"><Relationship Id="rId1" Type="{R}/officeDocument" Target="word/document.xml"/></Relationships>'''
    types=f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="{CT}"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/></Types>'''
    with ZipFile(OUT/name,'w',ZIP_DEFLATED) as z:
        add_entry(z,'[Content_Types].xml',types)
        add_entry(z,'_rels/.rels',rootrels)
        add_entry(z,'word/document.xml',document)
        add_entry(z,'word/styles.xml',styles)
        add_entry(z,'word/settings.xml',settings)
        add_entry(z,'word/_rels/document.xml.rels',rels)

# 1 official letter
make('01-official-letter.docx',
    table([
        ['BỘ XÂY DỰNG','CỤC HÀNG HẢI VÀ ĐƯỜNG THỦY VIỆT NAM'],
        ['CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM','Độc lập - Tự do - Hạnh phúc']
    ])+
    p('Số: 123/CHHĐTVN-VP')+p('Hà Nội, ngày 18 tháng 9 năm 2026',italic=True)+
    p('V/v triển khai kiểm tra văn bản',bold=False)+p('Kính gửi: Các đơn vị trực thuộc')+
    p('Thực hiện nhiệm vụ được giao, đề nghị các đơn vị triển khai.')+
    p('KT. CỤC TRƯỞNG')+p('PHÓ CỤC TRƯỞNG')+p('Nguyễn Văn An')+
    p('Nơi nhận:')+p('- Như trên;')+p('Lưu: VT, VP.')
)

# 2 named decision
make('02-named-decision.docx',
    p('CỤC HÀNG HẢI VÀ ĐƯỜNG THỦY VIỆT NAM')+p('CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM')+p('Độc lập - Tự do - Hạnh phúc')+
    p('Số: 88/QĐ-CHHĐTVN')+p('Hà Nội, ngày 18 tháng 9 năm 2026')+
    p('QUYẾT ĐỊNH',bold=True)+p('Về việc ban hành Quy chế thử nghiệm',bold=True)+
    p('Căn cứ Nghị định số 30/2020/NĐ-CP;')+p('Điều 1. Ban hành kèm theo Quyết định này Quy chế thử nghiệm.')+
    p('1. Phạm vi áp dụng.')+p('a) Đối tượng áp dụng.')+p('CỤC TRƯỞNG')+p('Nguyễn Văn Bình')
)

# 3 ambiguous named + official-letter subject
make('03-ambiguous-type.docx',
    p('CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM')+p('Độc lập - Tự do - Hạnh phúc')+p('Số: 09/QĐ-ABC')+
    p('QUYẾT ĐỊNH')+p('V/v nội dung bị mâu thuẫn')+p('Kính gửi: Đơn vị A')+p('Nội dung.')
)

# 4 table-based header + report
make('04-table-header.docx',
    table([
        ['TỔNG CÔNG TY A','CÔNG TY B'],
        ['CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM','Độc lập - Tự do - Hạnh phúc']
    ])+p('Số: 45/BC-CTB')+p('Hải Phòng, ngày 18 tháng 9 năm 2026')+p('BÁO CÁO')+p('Kết quả công tác 9 tháng')+p('Nội dung báo cáo.')
)

# 5 unusual spacing and blanks
make('05-unusual-spacing.docx',
    p('CÔNG TY B',before='600',after='600')+p('')+p('CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM',after='720')+p('Độc lập - Tự do - Hạnh phúc')+
    p('')+p('Số: 12/TB-CTB')+p('Hải Phòng, ngày 18 tháng 9 năm 2026')+p('')+p('THÔNG BÁO',before='1000')+p('Về lịch làm việc')+p('Nội dung thông báo.')
)

# 6 multiple named type candidates
make('06-multiple-type-candidates.docx',
    p('CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM')+p('Độc lập - Tự do - Hạnh phúc')+p('Số: 10/TB-ABC')+
    p('THÔNG BÁO')+p('BÁO CÁO')+p('Nội dung có hai tiêu đề cạnh tranh.')
)

# 7 appendix
make('07-appendix.docx',
    p('PHỤ LỤC I',bold=True)+p('DANH MỤC HỒ SƠ',bold=True)+p('(Kèm theo Quyết định số 88/QĐ-ABC ngày 18 tháng 9 năm 2026)')+
    table([['STT'],['Tên hồ sơ']])+p('Nội dung phụ lục.')
)

# 8 copy form
make('08-copy.docx',
    p('SAO Y',bold=True)+p('CÔNG TY B')+p('Số: 18/SY-CTB')+p('Hải Phòng, ngày 18 tháng 9 năm 2026')+
    p('KT. GIÁM ĐỐC')+p('PHÓ GIÁM ĐỐC')+p('Nguyễn Văn Cường')
)

# 9 hierarchy
make('09-hierarchy.docx',
    p('QUY ĐỊNH')+p('Về cấu trúc thử nghiệm')+p('PHẦN I')+p('QUY ĐỊNH CHUNG')+p('CHƯƠNG I')+p('MỤC 1')+
    p('TIỂU MỤC 1')+p('Điều 1. Phạm vi điều chỉnh')+p('1. Nội dung khoản.')+p('a) Nội dung điểm.')
)

# 10 missing / unknown
make('10-missing-components.docx', p('Đây chỉ là một đoạn nội dung không có dấu hiệu nhận diện loại văn bản.'))

# 11 markings/signature/contact
make('11-markings-signature.docx',
    p('KHẨN')+p('MẬT')+p('LƯU HÀNH NỘI BỘ')+p('THÔNG BÁO')+p('Về công việc')+
    p('KT. GIÁM ĐỐC')+p('PHÓ GIÁM ĐỐC')+p('Trần Văn Dũng')+p('Số lượng bản phát hành: 05')+
    p('Địa chỉ: 01 Đường A; Điện thoại: 0123456789; email: vanthu@example.com')
)

expected={
  '01-official-letter.docx': {'class':'OfficialLetter','specific':'cong_van','review':False,'template':'ND30.PL3.TEMPLATE_1_5','roles':['NationalHeader','NationalMotto','DocumentNumber','DocumentNotation','IssuePlaceAndDate','SubjectOfficialLetter','Addressee','IssuingAuthority','RecipientList','RetentionLine']},
  '02-named-decision.docx': {'class':'NamedAdministrativeDocument','specific':'quyet_dinh','review':True,'template':None,'roles':['DocumentTypeHeading','SubjectNamedDocument','LegalBasisBlock','ArticleHeading','Clause','Point']},
  '03-ambiguous-type.docx': {'class':'Unknown','specific':None,'review':True,'template':None,'roles':['DocumentTypeHeading','SubjectOfficialLetter','Addressee']},
  '04-table-header.docx': {'class':'NamedAdministrativeDocument','specific':'bao_cao','review':False,'template':'ND30.PL3.TEMPLATE_1_4','roles':['NationalHeader','NationalMotto','IssuingAuthorityParent','IssuingAuthority','DocumentTypeHeading','SubjectNamedDocument']},
  '05-unusual-spacing.docx': {'class':'NamedAdministrativeDocument','specific':'thong_bao','review':False,'template':'ND30.PL3.TEMPLATE_1_4','roles':['NationalHeader','NationalMotto','DocumentTypeHeading','SubjectNamedDocument']},
  '06-multiple-type-candidates.docx': {'class':'Unknown','specific':None,'review':True,'template':None,'roles':['DocumentTypeHeading']},
  '07-appendix.docx': {'class':'Appendix','specific':'phu_luc','review':True,'template':None,'roles':['AppendixNumber','AppendixTitle','AppendixReference']},
  '08-copy.docx': {'class':'Copy','specific':'ban_sao','review':True,'template':None,'roles':['CopyFormHeading','CopyAuthority','SigningAuthorityPrefix','SignerName','CopyCertificationSignature']},
  '09-hierarchy.docx': {'class':'NamedAdministrativeDocument','specific':'quy_dinh','review':False,'template':'ND30.PL3.TEMPLATE_1_4','roles':['PartHeading','ChapterHeading','SectionHeading','SubsectionHeading','ArticleHeading','Clause','Point']},
  '10-missing-components.docx': {'class':'Unknown','specific':None,'review':True,'template':None,'roles':['Body']},
  '11-markings-signature.docx': {'class':'NamedAdministrativeDocument','specific':'thong_bao','review':False,'template':'ND30.PL3.TEMPLATE_1_4','roles':['UrgencyMark','ClassificationMark','CirculationInstruction','SigningAuthorityPrefix','SignerTitle','SignerName','IssuedCopyCount','ContactInformation']}
}
(OUT/'expected.json').write_text(json.dumps(expected,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('semantic fixtures:',len(list(OUT.glob('*.docx'))))
