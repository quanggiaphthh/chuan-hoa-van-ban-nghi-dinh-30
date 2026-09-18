# Migration Plan

## Mục tiêu

Chuyển repository từ bộ Markdown hướng dẫn sang kiến trúc v2 mà không làm mất lịch sử và không sửa nội dung cũ trước khi rule/source model ổn định.

## GĐ0 — Foundation

- Giữ nguyên toàn bộ `nghi-dinh-30/` và placeholder hiện có.
- Bổ sung Architecture, Source Model, Rule Model, Document Model, Applicability, Validation và Fix Safety.
- Bổ sung JSON Schema và Source Registry.
- Chưa tạo production DOCX parser/formatter.

## GĐ1 — Audit và migrate 7 file Nghị định 30

1. Đối chiếu toàn bộ statement với NĐ30 + Phụ lục I–VI.
2. Sửa P0/P1 trước.
3. Tách normative statement khỏi commentary/recommendation.
4. Chuyển rule machine-readable sang `rules/`.
5. Chuyển guide cũ sang `docs/commentary/` hoặc generated docs.
6. `07-checklist-kiem-tra.md` về lâu dài phải được sinh từ verified rules, không maintain thủ công.

## Mapping dự kiến

| File hiện tại | Đích v2 |
|---|---|
| `01-nguyen-tac-chung.md` | commentary/overview + rules |
| `02-the-thuc-van-ban.md` | rules + generated docs |
| `03-ky-thuat-trinh-bay.md` | formatting rules + generated docs |
| `04-bo-cuc-noi-dung.md` | structure rules + commentary |
| `05-cac-loai-van-ban.md` | source/rules + generated table |
| `06-huong-dan-soan-thao.md` | workflow rules + commentary |
| `07-checklist-kiem-tra.md` | generated checklist |

## GĐ2 — Full NĐ30 coverage

- Map 38 điều + 6 phụ lục.
- Mỗi mandatory rule có source locator.
- Có coverage manifest: extracted/reviewed/verified/tested.

## GĐ3+ — Domain mở rộng

- electronic transaction/signature;
- records/archive;
- Party regime;
- organization profiles;
- DOCX Document Engine;
- validator và safe patch engine.

## Gate

Không merge migration lớn vào `main` nếu schema/routing/source tests chưa PASS.
