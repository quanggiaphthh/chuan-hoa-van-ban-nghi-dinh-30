# Technology Decisions (ADR Index)

Các quyết định dưới đây khóa định hướng GĐ0 và tránh tranh luận lại không cần thiết trong từng PR.

## ADR-001 — JSON Schema Draft 2020-12

Dùng JSON Schema Draft 2020-12 cho Source, Rule, Rule Pack, Profile và các contract khác.

## ADR-002 — YAML authoring, JSON compiled output

Canonical authoring dùng YAML/JSON schema-validated. Runtime có thể compile sang JSON; runtime không cần đọc Markdown.

## ADR-003 — Akoma Ntoso không phải canonical model

Không dùng Akoma Ntoso làm format lõi. Có thể bổ sung adapter import/export sau này.

## ADR-004 — Provenance nội bộ, không RDF runtime

Học mô hình Entity/Activity/Agent của W3C PROV nhưng chưa đưa RDF/OWL/SPARQL vào GĐ0.

## ADR-005 — Document Engine: .NET 10 LTS

Khuyến nghị production Document Engine dùng .NET 10 LTS.

## ADR-006 — DOCX adapter: Open XML SDK

Dùng Open XML SDK làm tầng truy cập package/OOXML. SDK không chứa logic Nghị định 30.

## ADR-007 — Legal rules độc lập Open XML

Canonical Rules chỉ thao tác trên Document Model, không XPath/w:p/w:r trực tiếp.

## ADR-008 — Không database trong GĐ0

Git + YAML/JSON + schema đủ cho GĐ0. Chỉ cân nhắc database khi quy mô/runtime yêu cầu.

## ADR-009 — Không phát triển trực tiếp trên main

Thay đổi kiến trúc/schema thực hiện trên feature branch và review bằng PR.

## ADR-010 — Signed documents audit-only by default

Nếu phát hiện tài liệu đã ký số, validator được phép đọc/audit nhưng auto-fix bị từ chối mặc định.

## ADR-011 — Deterministic core

Schema validation, date filtering, rule selection, exact constraint checks và patch execution phải deterministic. LLM chỉ hỗ trợ classification/semantic review khi cần.
