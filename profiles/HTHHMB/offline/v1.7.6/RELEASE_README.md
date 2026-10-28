# HTHHMB — Quy tắc offline NĐ30/HD05 v1.7.6

**Phạm vi của bản commit GitHub:** bản xuất **dữ liệu văn bản có thể đọc độc lập** (CSV/Markdown/JSON và script QA), lưu theo phiên bản, không chỉnh sửa các rule packs và profile cũ của repository.

> **Lưu ý về gói nhị phân:** `BO_RULE_PROFILE_HTHHMB_v1.7.6_OFFLINE_FULL.xlsx` và `BO_RULE_OFFLINE_HTHHMB_v1.7.6_FULL.zip` được kiểm tra tại nơi tạo nhưng **không có trong commit này** vì kết nối GitHub phiên hiện tại không hỗ trợ chuyển bytes trực tiếp từ tệp cục bộ. Không coi các tệp đã có trên GitHub; SHA-256 bên dưới chỉ là danh tính của bản gốc.

## Bắt đầu

1. Đọc [READ_FIRST.md](READ_FIRST.md) để chọn đúng hệ NĐ30/HD05, điều kiện và ngoại lệ.
2. [RULE_ALL_ATTRIBUTES.csv](RULE_ALL_ATTRIBUTES.csv) là 868 thuộc tính bổ sung của C/N/G/X; phần D (434 trường con) được kế thừa trong workbook gốc và gói offline, **chưa được xuất riêng trên GitHub**.
3. [MA_TRAN_165.csv](MA_TRAN_165.csv) kiểm kê 165 trường cha; [AUDIT_BANG_132.csv](AUDIT_BANG_132.csv) đối chiếu 132 trường con bảng.
4. Đọc [BLOCKERS.md](BLOCKERS.md), [QA_CHECKLIST.md](QA_CHECKLIST.md), [TQA_NR.md](TQA_NR.md) trước khi kết luận tuân thủ.
5. [manifest.json](manifest.json) là manifest gốc của ZIP đầy đủ, **không phải kê khai rằng mọi file trong ZIP đã được commit ở đây**.

## Trạng thái

- Kết quả QA theo gói gốc: **STRUCTURAL_PASS_WITH_UNVERIFIED_AND_BLOCKED**.
- 13 pt và Single trong bảng HD05: lựa chọn profile, **UNVERIFIED** nếu khẳng định ngoại lệ pháp lý.
- Quy tắc lặp hàng tiêu đề: bắt buộc **theo profile**, không được suy thành quy định bắt buộc phổ quát.
- TQA-NR: proxy về khả năng đọc và tính nhất quán, **không có điểm thẩm mỹ hiệu chỉnh**.
- Không tự suy ký hiệu cơ quan, quyết định phê duyệt, chữ ký, con dấu; không chỉnh 209 template hoặc phát triển formatter.

## Kiểm tra danh tính gói đầy đủ

```text
BO_RULE_PROFILE_HTHHMB_v1.7.6_OFFLINE_FULL.xlsx
SHA256 5aa663404b5e211fc23ba2cac7b74e21b7bf314e80fc223862d966183e4fc186

BO_RULE_OFFLINE_HTHHMB_v1.7.6_FULL.zip
SHA256 16990a8dc696bcae702f175279c95e8e05348856c8d938f30ee449cef52c9861
```

Khi có ZIP gốc trong máy, chạy `python VERIFY_PACKAGE.py /path/to/BO_RULE_OFFLINE_HTHHMB_v1.7.6_FULL.zip`; script kiểm tra dữ liệu gói đầy đủ, không phải kiểm tra rằng toàn bộ file đã tồn tại trong repository.

**Nguồn:** Nghị định 30/2020/NĐ-CP và Hướng dẫn 05-HD/VPTW (2026) được dẫn trong [READ_FIRST.md](READ_FIRST.md). Quy tắc có chứng cứ, profile đã chốt và mục chưa xác minh phải luôn được phân biệt.
