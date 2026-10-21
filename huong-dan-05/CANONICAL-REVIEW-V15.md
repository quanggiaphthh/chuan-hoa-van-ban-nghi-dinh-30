# V15 — Thẩm định trích xuất 12 quy tắc nguyên tử Hướng dẫn 05

**Ngày:** 19/10/2026  
**Nhánh:** `research/hd05-v15-atomic-candidates`, tách từ hồ sơ tài liệu PR #10.  
**Quyết định:** `EXTRACTED_CANDIDATE_SCHEMA_PASS` · `INDEPENDENT_LEGAL_REVIEW_PENDING` · `RUNTIME_NO_GO` · `AUTO_FIX_NO_GO`.

> Đây là **đợt trích xuất và kiểm chứng cấu trúc**, chưa phải 12 quy tắc đã được phê duyệt. Maturity của từng tệp là `extracted`, mọi chính sách `autofix` đều `PROHIBITED`. Các tệp nằm tại `huong-dan-05/candidates-v15/`, **không thuộc** `v22-runtime/rules/` hoặc `v22-runtime/release/`.

## 1. Nguồn gốc trực tiếp

- [Hướng dẫn 05-HD/VPTW ngày 27/05/2026, PDF 36 trang](https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf), Phụ lục 2 trang PDF 31–32, và Phần II, mục IV.4.1 trang PDF 28. Đã kiểm tra cả text và ảnh bảng gốc để tránh nhầm ô/bảng.
- [Quy định 399-QĐ/TW ngày 09/01/2026](https://xaydungchinhsach.chinhphu.vn/quy-dinh-so-399-qd-tw-cua-ban-bi-thu-ve-the-loai-tham-quyen-ban-hanh-va-the-thuc-van-ban-cua-dang-119260119124508363.htm) xác định chế độ/thể loại; ứng dụng phải giải quyết thẩm quyền, loại văn bản và hiệu lực trước khi đánh giá.
- Contract hiện có: `schemas/rule.schema.json`, `RULE-MODEL.md`, `APPLICABILITY.md`, `sources/registry.yaml`, `rule-packs/party-document-format-2026.yaml`; hiện không có verified Party release.
- `DAC-TA-CHUAN-V12.md`, `ATOMIC-REUSE-AUDIT-V13.md` và `P0-READINESS-V14.md` là hồ sơ nghiên cứu, không thay thế nguồn quy phạm.

## 2. Ma trận 12 nguyên tử ứng viên

| Tệp số | ID rút gọn | Giá trị nguồn | PDF/truy vết | Ánh xạ V13 | Điều kiện chưa giải quyết |
|---|---|---|---|---|---|
| 01 | `PL2.ROW01.PARTY_HEADING.SIZE` | 15 pt | PL2 TT1, tr.31 | `R01.01` | Role heading trong DOCX và effective-run span |
| 02 | `PL2.ROW01.PARTY_HEADING.BOLD` | Đậm | PL2 TT1, tr.31 | `NEW-V15.01` | Chưa nằm trong 59 property slots V13 |
| 03 | `PL2.ROW02.ISSUER.SIZE` | 14 pt | PL2 TT2, tr.31 | `NEW-V15.02` | Phân biệt cấp trên trực tiếp và đơn vị ban hành |
| 04 | `PL2.ROW03.NUMBER.SIZE` | 14 pt | PL2 TT3, tr.31 | `NEW-V15.03` | Không đồng nhất với số thứ tự văn thư |
| 05 | `PL2.ROW05.TYPE.SIZE` | 15–16 pt | PL2 TT5, tr.31 | `R08.01` | Chỉ văn bản có tên loại, không áp vào trích yếu công văn |
| 06 | `PL2.ROW05.NAMED_SUBJECT.SIZE` | 14–15 pt | PL2 TT5, tr.31 | `R09.01` | Cần nhận diện đúng trích yếu dưới tên loại |
| 07 | `PL2.ROW05.LETTER_SUBJECT.SIZE` | 12 pt | PL2 TT5, tr.32 | `R10.01` | Chỉ công văn, không suy thành font thân văn bản |
| 08 | `PL2.ROW06.BODY.SIZE` | 14–15 pt | PL2 TT6, tr.32 | `R12.01` | Không áp cho tiêu đề phần/chương/mục hoặc phần cuối |
| 09 | `PARTII.IV.4.1.1.MARGIN_TOP` | 20 mm | Phần II.IV.4.1.1, tr.28 | `R22.01` | Khổ/section, hướng giấy và mặt giấy |
| 10 | `PARTII.IV.4.1.2.MARGIN_BOTTOM` | 20 mm | Phần II.IV.4.1.2, tr.28 | `R22.02` | Khổ/section, hướng giấy và mặt giấy |
| 11 | `PARTII.IV.4.1.3.MARGIN_LEFT` | 30 mm | Phần II.IV.4.1.3, tr.28 | `R22.03` | **Mặt sau in hai mặt là 15 mm**, không áp nhầm |
| 12 | `PARTII.IV.4.1.4.MARGIN_RIGHT` | 15 mm | Phần II.IV.4.1.4, tr.28 | `R22.04` | **Mặt sau in hai mặt là 30 mm**, không áp nhầm |

**Ánh xạ:** 9 thuộc tính kế thừa từ V13, 3 thuộc tính `NEW-V15.01–03` được bổ sung có chủ đích. Không đánh đồng 12 bản ghi này với toàn bộ 59 slot ứng viên V13. Hai khái niệm `PL1.BOX` và `PL2.ROW` được giữ riêng: các ID ở đây sử dụng ROW, không phải vị trí ô trên sơ đồ Phụ lục 1.

## 3. Lựa chọn kỹ thuật và những gì KHÔNG được khẳng định

1. **Contract bất biến:** `target` trong schema chỉ chấp nhận `object_type` và `property`; vì vậy các quy tắc hiển thị vai trò dưới `extensions.semantic_role` và `target.property` có tên property mang ngữ cảnh (ví dụ `party_heading.font_size_pt`). Không mở rộng schema để hợp thức hoá một trường `target.role` chưa được phép.
2. **Maturity:** `extracted` chỉ có nghĩa trích xuất và kiểm tra hình thức, không có nghĩa chuyên gia đã ký duyệt; chưa có `reviewed` hoặc `verified`. `normativity: guidance` là lựa chọn phân loại tạm thời theo Rule Model, không giảm hiệu lực nguồn có thẩm quyền.
3. **Applicability:** tạm yêu cầu regime Party và loại `official_party_document`. Vai trò `named`, `official_letter`, `page_side=front_or_single` là *tín hiệu kỹ thuật dự kiến*, cần được chứng minh từ document context và thẩm định ngoại lệ văn bản, giấy tờ hành chính A5/mẫu in sẵn, in hai mặt mặt sau, section nằm ngang chứa bảng.
4. **Định dạng:** xác minh bằng **effective formatting trên toàn bộ các run/cụm** thuộc trường chứ không phải chỉ một run; block provenance phải đúng vùng front/body/end/appendix. Chưa có tích hợp capability này trong Party runtime.
5. **Lề:** số đo bốn cạnh của bản trình bày chiều dọc tại §4.1; §4.2 mặt sau in hai mặt hoán đổi lề trái/phải, §4.3 có ngoại lệ bảng/biểu nằm ngang. Chưa tạo rule riêng cho mặt sau hoặc ngang trong đợt này để tránh tự suy điều kiện thiếu evidence.
6. **Bảo toàn:** không chạm DOCX, tài liệu có chữ ký số, stamp, ngày cấp và số văn thư. `autofix: PROHIBITED` cho mọi candidate.

## 4. Kiểm thử có thể tái lập

Script `candidates-v15/verify_candidates.py` tái sử dụng `schemas/rule.schema.json` qua thư viện JSON Schema chuẩn và PyYAML; kiểm từng tệp theo cấu trúc 2020-12, ID duy nhất, nguồn, điều kiện, giá trị pháp lý, ngày hiệu lực và dấu chặn tự sửa. Script kiểm tiếp release hiện có vẫn 435 ND30 IDs, không lẫn ứng viên Party.

```bash
python -m pip install PyYAML==6.0.3 jsonschema==4.25.1
python huong-dan-05/candidates-v15/verify_candidates.py
```

Workflow CI độc lập: `.github/workflows/hd05-v15-rule-candidate-audit.yml`.

Bản đầu của CI bị chặn vì `target.role` vi phạm `unevaluatedProperties: false` của schema thực tế; **đã sửa dữ liệu theo contract hiện hữu**, không sửa schema để tránh phải cấp lại kiểm thử hệ thống. Các log CI fail là chứng cứ phát hiện lỗi và không được xoá bỏ trong lịch sử PR.

## 5. Gate thẩm định tiếp theo

| Gate | Điều kiện để chuyển trạng thái | Hiện trạng |
|---|---|---|
| Static JSON Schema | Đúng `rule.schema.json`, locator, 12 giá trị, scope, safety | Đã PASS trên CI (cần đọc log mới nhất của PR) |
| Legal exactness | Hai người review độc lập PDF theo ô/hàng, phân xử `normativity`, ngày hiệu lực và ngoại lệ | **Chưa** |
| Exact dedup | So ID, source và atomic constraints với toàn bộ repo/current branch trước khi đăng ký canonical | ID ứng viên mới; **chưa có review pháp lý toàn bộ** |
| Observable semantics | Role spans và effective formatting, fallback khi ambiguities | **Chưa** |
| Renderer/section evidence | DOCX mixed styles, section parity, two-sided/landscape cases | **Chưa** |
| Runtime release / manifest | Chỉ rule verified được cấp phép và có authority | **NO-GO** |
| Auto-fix | Mở lại DOCX, chứng minh thay đổi cục bộ và bảo toàn toàn gói, phê duyệt | **NO-GO** |

**Đề xuất chính thức:** Giữ cả 12 ở trạng thái `extracted`. Sau khi độc lập rà nguồn/role/applicability, có thể chọn một ứng viên để thực nghiệm **chỉ phát hiện lỗi**, nhưng không gắn verified hay mở auto-fix chỉ vì schema và CI đạt.
