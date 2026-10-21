# Hướng dẫn 05-HD/VPTW (2026) — hồ sơ nghiên cứu và đối chiếu

**Trạng thái:** `REVIEW_READY / DOCUMENTATION_ONLY` — chưa được phê duyệt làm canonical rules hoặc bật auto-fix.

Thư mục này chứa tài liệu nghiên cứu về thể thức và kỹ thuật trình bày văn bản của Đảng. Không phải một engine mới và không thay thế văn bản chính thức.

## Tài liệu

| Tệp | Công dụng |
|---|---|
| [DAC-TA-CHUAN-V12.md](./DAC-TA-CHUAN-V12.md) | Đặc tả hợp nhất V12: 17 mục; mô hình 9 thành phần thể thức; phân loại 25+8; phụ lục/bản sao; 29 nhóm quy tắc ứng viên; 88 tình huống QA được đề xuất |
| [GAP-AUDIT-V12.md](./GAP-AUDIT-V12.md) | Đối chiếu REUSE FIRST với `source registry`, `rule pack`, `document model`, `rule model`, `applicability`; xác định các thành phần còn thiếu hoặc bị chặn |
| [ATOMIC-REUSE-AUDIT-V13.md](./ATOMIC-REUSE-AUDIT-V13.md) | Kiểm toán tĩnh 29 nhóm → 59 thuộc tính nguyên tử ứng viên; bằng chứng reuse thực tế, giới hạn detector/parser/loader và điều kiện kiểm thử; **chưa là canonical rule** |
| [P0-READINESS-V14.md](./P0-READINESS-V14.md) | Kiểm toán sâu ba điểm nghẽn P0: release routing, Party semantic detection, thứ tự đoạn–bảng; yêu cầu thay đổi tối thiểu và gate test theo blast radius |
| [CANONICAL-REVIEW-V15.md](./CANONICAL-REVIEW-V15.md) | Hồ sơ thẩm định 12 quy tắc nguyên tử ở trạng thái extracted; truy vết Phụ lục 2 và Phần II.IV.4.1, bất biến schema và giới hạn cấp quyền |
| [candidates-v15/](./candidates-v15/) | 12 tệp YAML ứng viên theo schema hiện hữu và script kiểm tra; **không nằm trong runtime/rule pack, không verified, không auto-fix** |

## Nguồn có thẩm quyền (ưu tiên trước tài liệu diễn giải)

1. [Hướng dẫn 05-HD/VPTW ngày 27/05/2026 — PDF 36 trang](https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf), gồm Phụ lục 1 (sơ đồ), Phụ lục 2 (bảng thông số) và Phụ lục 3 (bản sao giấy).
2. [Quy định 399-QĐ/TW ngày 09/01/2026](https://xaydungchinhsach.chinhphu.vn/quy-dinh-so-399-qd-tw-cua-ban-bi-thu-ve-the-loai-tham-quyen-ban-hanh-va-the-thuc-van-ban-cua-dang-119260119124508363.htm), Điều 5–7 và 14–17.

**Ưu tiên nguồn:** văn bản gốc/hiệu lực pháp lý → canonical rule `verified` có locator → bản đặc tả hỗ trợ kiểm toán. Nếu có khác biệt, không dùng V12 để override nguồn chính thức.

## Cách sử dụng an toàn

- Quy định 399 (09/01/2026) và Hướng dẫn 05 (27/05/2026) có mốc hiệu lực riêng; xác định regime, issuer, document type, issue date trước khi áp rule.
- 3 vùng `front/body/end` và các vùng `appendices[]`, `overlays[]`, `copy_metadata`, `unclassified[]` là **mô hình phân tích DOCX**, không phải 6 phần pháp lý bắt buộc.
- Nhận diện `Kính gửi` của công văn và `Kính trình` của tờ trình theo **vai trò nơi nhận** và vị trí đặc thù; không trộn vào nội dung.
- Không tự sửa ngày ký, số văn bản, nơi nhận, dấu, chữ ký, tài liệu đã ký hoặc tài liệu chưa được phép xử lý.
- `NOT_EVALUATED`/ `NEEDS_REVIEW` không được chuyển thành `PASS`; 88 QA trong tài liệu **chưa chạy** ở lượt cập nhật này.

## Tích hợp REUSE FIRST

Trước khi bổ sung rule hoặc component, đối chiếu với:

- [Source Registry](../sources/registry.yaml), [Party Rule Pack](../rule-packs/party-document-format-2026.yaml)
- [Rule Model](../RULE-MODEL.md), [Document Model](../DOCUMENT-MODEL.md), [Applicability](../APPLICABILITY.md), [Validation Results](../VALIDATION-RESULTS.md)

Không tạo subsystem song song, không tạo canonical rules từ 29 mã `R01–R29` hoặc 59 property slots ứng viên nếu chưa exact-dedup và kiểm chứng. Đóng góp thay đổi source/rule/runtime bằng PR riêng và bằng chứng kiểm thử theo blast radius.

---
*Tài liệu nghiên cứu; các mốc và quy tắc phải được thẩm định theo nguồn có thẩm quyền trước khi dùng làm căn cứ tự động hóa.*
