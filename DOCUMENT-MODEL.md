# Document Model

## Mục tiêu

Document Model là mô hình trung gian trung lập với Word/Open XML. Rule pháp lý không được phụ thuộc trực tiếp vào `w:p`, `w:r`, XPath hoặc chỉ số paragraph.

## Cấu trúc khái niệm

```text
Document
 ├─ sections[]
 ├─ semantic_components[]
 ├─ paragraphs[]
 ├─ tables[]
 ├─ headers[]
 ├─ footers[]
 ├─ styles
 ├─ numbering
 └─ signatures / protections / revisions
```

Mỗi node nên có:

```yaml
id:
type:
text:
effective_properties:
source_evidence:
```

## Semantic components

Các role ban đầu:

- `national_header`
- `motto`
- `issuing_authority`
- `document_number`
- `issue_place_and_date`
- `document_type`
- `subject`
- `body`
- `signer_title`
- `signer_name`
- `recipients`

## Effective properties

Validator kiểm **giá trị hiệu dụng**, không chỉ raw XML. Document Engine tương lai phải resolve document defaults, style inheritance, paragraph/run properties và direct formatting trước khi đưa dữ liệu vào model.

Các thuộc tính chuẩn hoá dùng đơn vị thân thiện:

```yaml
font_family: Times New Roman
font_size_pt: 14
margin_left_mm: 30
line_spacing: 1.2
```

Open XML adapter chịu trách nhiệm chuyển twips/half-points và các đơn vị OOXML sang canonical unit.

## Section-aware layout

Lề, orientation và page size là thuộc tính theo section. Model phải giữ `sections[]` và role của section để rule có thể cho phép ngoại lệ hợp lệ, ví dụ trang ngang cho bảng/biểu lớn.

## Evidence

Mỗi kết luận phải có evidence đủ để audit:

```yaml
source_evidence:
  format: docx
  part: word/document.xml
  paragraph_ref: p-12
  run_refs: [r-38]
```

Nếu semantic role được AI suy luận, model phải ghi phương pháp và mức tin cậy phân loại (`CONFIRMED`, `INFERRED_HIGH`, `INFERRED_LOW`, `UNKNOWN`).

## Capability awareness

Rule cần khai báo capability mà validator/document engine phải có. Nếu engine chưa hỗ trợ, kết quả là `NOT_EVALUATED`, không được giả `PASS`.
