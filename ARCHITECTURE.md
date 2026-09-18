# Kiến trúc v2

## Mục tiêu

Dự án được định hướng từ bộ ghi chú Markdown thành một **Document Standards Registry** có thể truy xuất nguồn, kiểm thử và sử dụng cho AI/validator DOCX.

## Nguyên tắc kiến trúc

1. Markdown hướng dẫn không phải source of truth.
2. Nguồn chính thức được đăng ký trong `sources/`.
3. Quy tắc chuẩn hoá được biểu diễn thành các rule nguyên tử trong `rules/`.
4. Áp dụng rule dựa trên `Regime × Domain × Applicability × Time`.
5. Rule pháp lý, profile nội bộ và khuyến nghị chất lượng phải tách biệt.
6. DOCX được chuyển thành Document Model trung gian trước khi đánh giá rule.
7. Auto-fix chỉ được thực hiện với thay đổi an toàn, cục bộ và có thể truy vết.

## Luồng tổng thể

```text
Official / verified sources
        ↓
Source Registry
        ↓
Atomic Canonical Rules
        ↓
Applicability + Temporal Resolver
        ↓
Minimal Rule Packs
        │
        ├──────────── Organization Profile
        │
DOCX → Document Engine
        │
        └─────────────┬──────────────
                      ↓
                  Validator
                      ↓
             Reversible safe patches
                      ↓
               Structural + visual QA
```

## Regime chính

- `administrative`: văn bản hành chính / công tác văn thư.
- `normative_legal`: văn bản quy phạm pháp luật.
- `party`: văn bản của cấp uỷ, tổ chức và cơ quan Đảng.
- `specialized`: văn bản chuyên ngành.
- `unknown`: chưa đủ dữ liệu để phân loại; không cho phép structural auto-fix.

## Domain chính

- `document_format`
- `document_structure`
- `document_workflow`
- `electronic_transaction`
- `electronic_signature`
- `file_interoperability`
- `records_management`
- `archival_preservation`
- `data_governance`
- `information_security`
- `legal_authority`

## Contract lõi

```text
SOURCE CONTRACT
      ↓
RULE CONTRACT
      ↓
CONTEXT CONTRACT
      ↓
DOCUMENT CONTRACT
      ↓
VALIDATION CONTRACT
      ↓
PATCH CONTRACT
```

## Phạm vi GĐ0

GĐ0 xây dữ liệu, schema, routing, temporality, rule semantics, profile semantics, validation semantics và safety contract. GĐ0 **không** triển khai production DOCX parser, Word formatter, database, API, UI hay LLM runtime.

## Baseline

Baseline thiết kế v2 được đóng từ commit `6f5a5f1d38a18b6117d8054134e76e8df472d10a` ngày 18/09/2026. Các tài liệu cũ trong `nghi-dinh-30/` được giữ nguyên trong GĐ0 để phục vụ migration có kiểm soát ở GĐ1.
