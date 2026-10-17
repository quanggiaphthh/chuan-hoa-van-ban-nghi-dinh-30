# Kiểm toán khoảng trống và ánh xạ quy tắc HD05 — V12

**Ngày đối chiếu:** 17/10/2026  
**Baseline GitHub:** `078dae21f9e23975676179fa189f5c01b605f49a` (`main` khi bắt đầu)  
**Trạng thái:** `DOCUMENTATION_ONLY / REVIEW_REQUIRED / NO_RUNTIME_APPROVAL`.

## 1. Nguồn đã đối chiếu

- [Quy định 399-QĐ/TW ngày 09/01/2026](https://xaydungchinhsach.chinhphu.vn/quy-dinh-so-399-qd-tw-cua-ban-bi-thu-ve-the-loai-tham-quyen-ban-hanh-va-the-thuc-van-ban-cua-dang-119260119124508363.htm): Điều 5–7 (thể loại), Điều 14–17 (thể thức/bản sao).
- [Hướng dẫn 05-HD/VPTW ngày 27/05/2026](https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf): Phần I–III, Phụ lục 1 (ô bố trí), Phụ lục 2 (bảng định dạng), Phụ lục 3 (bản sao giấy).
- Nghiên cứu tổng hợp: [DAC-TA-CHUAN-V12.md](./DAC-TA-CHUAN-V12.md). Đây là **tài liệu ứng viên**, không thay văn bản có thẩm quyền và không phải canonical rule.

## 2. Audit REUSE FIRST (trên baseline)

| Authority/contract tại repo | Kết quả | Hướng xử lý |
|---|---|---|
| `sources/registry.yaml` | Có ID `VN-PARTY-QD399-2026` và `VN-PARTY-HD05-2026` | **REUSE**; không đăng ký ID trùng |
| `rule-packs/party-document-format-2026.yaml` | Đã include hai ID nguồn; **không chứng minh** runtime có 29 nhóm rule | **REUSE**, không nhân đôi rule pack |
| `RULE-MODEL.md`, `schemas/rule.schema.json` | Đã có canonical rule nguyên tử, normativity, source locator, maturity | **REUSE** contract; đề xuất mới phải exact-dedup |
| `DOCUMENT-MODEL.md` | Đã có section, semantic components và source evidence | **EXTEND** vai trò `party_heading`, `recipients_start`, `appendices[]` chỉ sau audit parser |
| `APPLICABILITY.md` | Có regime, thời gian, trạng thái và classification | **REUSE** routing; bổ sung điều kiện trường hợp, không viết resolver riêng |
| `VALIDATION-RESULTS.md` | Có `PASS/FAIL/NOT_APPLICABLE/NOT_EVALUATED/NEEDS_REVIEW` | **REUSE**; thiếu capability tuyệt đối không báo PASS |
| `huong-dan-05/README.md` trước thay đổi | Placeholder | **EXTEND** bằng chỉ dẫn sử dụng đặc tả V12 |
| Canonical rule HD05 có bằng chứng kiểm thử | Chưa định danh được rule-file/canonical ID tương ứng trên cây nguồn hiện tại | **MISSING/UNVERIFIED**; tuyệt đối chưa bật vào production |

**Phân biệt:** `implementation_status: core` trong Source Registry là metadata hiện có, **không phải** chứng nhận rule HD05 đã được chạy/verified.

## 3. Ánh xạ 29 nhóm tham chiếu của V12

Mã `R01–R29` là mã tài liệu V12, **không phải canonical ID**. Cột trạng thái phản ánh *khả năng tận dụng hợp đồng thiết kế*, không có nghĩa rule hoặc DOCX engine đã PASS.

| V12 | Trường/ý định kiểm tra | Reuse contract | Trạng thái trước triển khai |
|---|---|---|---|
| R01 | Cỡ chữ tiêu đề Đảng | Document Model + Rule Model | EXTEND / MISSING verified rule |
| R02 | Đường kẻ liền dưới tiêu đề | Structural + rendering evidence | BLOCKED: chưa có layout evidence |
| R03 | Dấu sao dưới cơ quan | Semantic roles + OOXML | EXTEND / MISSING verified rule |
| R04 | Số văn bản có chữ số 0 dẫn đầu | Context + numbering | BLOCKED: không thay sổ văn thư |
| R05 | Ký hiệu, dấu gạch trong số/ký hiệu | Context + parser metadata | BLOCKED: không sửa số đã cấp |
| R06 | Ngày dưới 10 | Context + temporal resolver | BLOCKED: không đổi ngày nghiệp vụ |
| R07 | Tháng dưới 3 | Context + temporal resolver | BLOCKED: phải đối chiếu ngày ban hành |
| R08 | Tên loại cỡ 15–16 pt | Effective formatting | EXTEND / MISSING verified rule |
| R09 | Trích yếu loại thường cỡ 14–15 pt | Effective formatting | EXTEND / MISSING verified rule |
| R10 | Trích yếu công văn 12 pt nghiêng | Document type + effective formatting | EXTEND / MISSING verified rule |
| R11 | Năm dấu gạch sau trích yếu | Metadata/boundary | EXTEND; không tự thêm cho công văn |
| R12 | Nội dung 14–15 pt | Body ownership + effective formatting | EXTEND / MISSING verified rule |
| R13 | Căn đều hai lề | Effective paragraph properties | EXTEND / MISSING verified rule |
| R14 | Thụt đầu dòng | Effective paragraph properties | EXTEND / MISSING verified rule |
| R15 | Khoảng cách đoạn | Effective paragraph properties | EXTEND / MISSING verified rule |
| R16 | Line spacing Exactly 18–22 pt | OOXML line mode + effective value | EXTEND / MISSING verified rule |
| R17 | Dấu mở đầu/kết thúc căn cứ | Body semantics | BLOCKED: review nội dung |
| R18 | Kính gửi công văn / Kính trình tờ trình | Type + position + semantic role | EXTEND; cần field ownership evidence |
| R19 | Nơi nhận và danh sách nơi nhận | Two distinct semantic fields | EXTEND; tách nguyên tử khi import |
| R20 | Số lượng và tiêu đề phụ lục | `sections[]` + `appendices[]` | EXTEND; chưa chứng minh round-trip |
| R21 | A4/A5 hoặc mẫu in có điều kiện | Applicability + section model | EXTEND / MISSING verified rule |
| R22 | 4 lề trang dọc | Section-aware properties | EXTEND; tách 4 atomic rules |
| R23 | Lề mặt sau giấy in hai mặt | Section + duplex intent | BLOCKED: không suy từ DOCX đơn lẻ |
| R24 | Số trang/định vị/ẩn trang đầu | Headers/footers/fields | BLOCKED: cần field+render evidence |
| R25 | Số trang phụ lục độc lập | Section/appendix numbering | BLOCKED: cần round-trip evidence |
| R26 | Dấu bản sao 80×50 mm | Copy evidence | BLOCKED: không xác nhận vật mang tin bằng OOXML |
| R27 | Màu đỏ, kiểu và cỡ chữ dấu bản sao | Copy evidence | BLOCKED: mực thật cần kiểm ngoài DOCX |
| R28 | Ký hiệu BS/sổ cấp số bản sao | Applicability + external registry | BLOCKED: không tự cấp số |
| R29 | Hai chữ ký số bản sao điện tử | Signed/protected safety contract | BLOCKED: thiếu chứng minh PKI |

## 4. Mốc nghiệm thu và giới hạn

- Nguồn pháp lý ở Phụ lục không tương đương engine coverage; trước khi thêm canonical rule phải có source locator, applicability theo thời gian và type, tính nguyên tử, luật chặn auto-fix.
- Không đổi `source registry`, `rule-pack`, `schemas`, `v22-runtime`, tests đã PASS trong PR tài liệu này.
- Tái sử dụng 88 tình huống QA trong V12: `T01–T30` (30), `TEST-G31–TEST-G46` (16), `G47–G88` (42). **Chưa chạy** 88 tình huống và không tuyên bố PASS.
- Các thay đổi rule/runtime tương lai chỉ nên mở trong PR khác khi có danh sách canonical IDs, fixture DOCX và báo cáo round-trip vùng ảnh hưởng; không đụng Locked Core nếu extension contract đủ.

## 5. Các việc vẫn mở

1. Xác nhận trạng thái hiệu lực và quan hệ alias của các công văn bị thay thế/bãi bỏ, đặc biệt Công văn 5053.
2. Đối chiếu giá trị quy phạm theo từng rule nguyên tử và gắn `source_locator` với phụ lục/trang tương ứng.
3. Kiểm tra actual runtime capability cho text/border/section/field/shape/footnote/AlternateContent, chữ ký số và trạng thái tài liệu.
4. Xác minh mapping 88 QA với fixtures đã có; chỉ tạo fixture mới nếu thiếu hành vi quan sát được.
5. Chỉ xem xét auto-fix sau khi có bằng chứng `FIX_VERIFIED` và phê duyệt người dùng.

---

**Kết quả kiểm toán hiện tại:** chỉ đủ căn cứ cập nhật **tài liệu nghiên cứu và ma trận khoảng trống**. Chưa đủ căn cứ tự động bổ sung `verified` canonical rules hoặc kích hoạt rule pack tại runtime.
