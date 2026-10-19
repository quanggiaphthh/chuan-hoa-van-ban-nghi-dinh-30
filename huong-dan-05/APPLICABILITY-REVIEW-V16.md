# V16 — Thẩm định phạm vi áp dụng của 12 ứng viên HD05

**Ngày đối chiếu:** 19/10/2026  
**Base:** PR #15 — `research/hd05-v15-atomic-candidates` (trạng thái `extracted`).  
**Thực thi:** PR V16 chỉ sửa 4 YAML lề, script QA và tài liệu.  
**Quyết định:** `SOURCE_VALUES_RECONFIRMED / APPLICABILITY_SPEC_CORRECTED / RUNTIME_NO_GO / AUTOFIX_PROHIBITED`.

## 1. Căn cứ và phương pháp

- **Hướng dẫn 05-HD/VPTW, bản PDF gốc 36 trang:** https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf
- Đã đối chiếu ảnh scan thực tế: PDF trang **28** (chỉ số trang 0-based `27`) cho Phần II.IV.4.1–4.3; PDF trang **31** (`30`) và **32** (`31`) cho Phụ lục 2, cỡ chữ và kiểu chữ.
- Trong khi Phụ lục 2 quy định kích thước từng trường, Phần II.IV.4.1–4.3 là **quy tắc phạm vi áp dụng đối với các section và mặt giấy**, không được giản lược mọi lề thành bốn giá trị áp vào mọi trang.
- Rà soát mã hiện hành: `SemanticValidationContextBuilder.AddFormatting()` ở `v22-runtime/src/LegalValidator/Adapters/` đang quan sát run đầu khả dụng; `EffectiveFormattingResolver` có thể được tái sử dụng để kiểm mọi run; `DocumentEngine/Model/Models.cs` có `SectionModel` với bốn giá trị lề nhưng **chưa thể hiện bằng chứng chắc chắn về mặt trước/mặt sau in hai mặt**, cũng chưa chứng minh rendered parity.

## 2. Phân xử 12 ứng viên

| ID số | Giá trị theo PDF | Giới hạn scope/ngữ nghĩa | Trạng thái |
|---|---|---|---|
| 01 | Tiêu đề Đảng 15 pt | Role chính xác; tiêu đề trong bảng có thể chỉ là evidence ứng viên; mixed runs → cần quan sát toàn bộ | `SOURCE_VALUE_CONFIRMED / OBSERVABILITY_BLOCKED` |
| 02 | Tiêu đề Đảng đứng, đậm | Ứng viên V15 chỉ kiểm đậm, **chưa kiểm đứng**; role + tất cả run | `SOURCE_VALUE_CONFIRMED / PARTIAL_COVERAGE` |
| 03 | Tên cơ quan ban hành 14 pt | Cần phân biệt tên cơ quan cấp trên và cơ quan ban hành, dù cả hai đều 14 pt; kiểu đậm khác nhau | `SOURCE_VALUE_CONFIRMED / PARTIAL_COVERAGE` |
| 04 | Số và ký hiệu 14 pt | Phải tách đúng metadata `Số ...` khỏi số đoạn/điều, không được tự đổi số đã cấp | `SOURCE_VALUE_CONFIRMED / OBSERVABILITY_BLOCKED` |
| 05 | Tên loại văn bản 15–16 pt | Không áp cho công văn không có tên loại; `heading_variant=named` chỉ là context đề xuất | `SOURCE_VALUE_CONFIRMED / CONTEXT_REVIEW` |
| 06 | Trích yếu có tên loại 14–15 pt | Phụ lục 2 cũng yêu cầu đứng, đậm, nhưng candidate mới kiểm cỡ chữ; không gọi là full style | `SOURCE_VALUE_CONFIRMED / PARTIAL_COVERAGE` |
| 07 | Trích yếu công văn 12 pt | Phụ lục 2 còn yêu cầu nghiêng; candidate hiện chỉ kiểm cỡ chữ, phải định tuyến `official_letter` | `SOURCE_VALUE_CONFIRMED / PARTIAL_COVERAGE` |
| 08 | Nội dung thông thường 14–15 pt | Không trộn các tiêu đề phần/chương/mục/điều với thân thường (cùng size có thể khác bold/italic) | `SOURCE_VALUE_CONFIRMED / OBSERVABILITY_BLOCKED` |
| 09 | Lề trên 20 mm | **Áp cho mặt trước và mặt sau** trang dọc, không cần lọc theo `page_side`; vẫn cần section orientation | `APPLICABILITY_CORRECTED` |
| 10 | Lề dưới 20 mm | **Áp cho mặt trước và mặt sau** trang dọc, không cần lọc theo `page_side`; vẫn cần section orientation | `APPLICABILITY_CORRECTED` |
| 11 | Lề trái 30 mm | **Chỉ cho mặt trước hoặc in một mặt**; mặt sau in hai mặt là 15 mm. Nếu không có duplex/parity evidence → `NOT_EVALUATED` | `FAIL_CLOSED_SCOPE_REINFORCED` |
| 12 | Lề phải 15 mm | **Chỉ cho mặt trước hoặc in một mặt**; mặt sau in hai mặt là 30 mm. Nếu không có duplex/parity evidence → `NOT_EVALUATED` | `FAIL_CLOSED_SCOPE_REINFORCED` |

**Kết luận về quy định:** theo Phần II.IV.4.1, bốn lề trang dọc mặt đầu/một mặt là `20/20/30/15 mm` (trên/dưới/trái/phải). Theo II.IV.4.2, mặt sau in hai mặt là `20/20/15/30 mm`. Theo II.IV.4.3, trang ngang dành cho bảng/biểu phải căn cứ vùng dọc để trình bày phù hợp; **không suy bốn lề ngang thành một cặp số mới**.

## 3. Kiểm toán khả năng quan sát trong DOCX — không suy từ schema PASS

### 3.1. Thẩm quyền/loại/ngày

Tối thiểu phải có `regime.value=party`, nguồn pháp lý theo **ngày ban hành**, loại tài liệu không thuộc nhóm loại trừ (văn bản chuyên ngành/ấn phẩm sách), cơ quan có thẩm quyền và khu vực/field role có provenance. `document.class=official_party_document` hiện là **từ vựng ứng viên**, chưa chứng minh đã được populated trong ValidationContext.

### 3.2. Mixed-runs và kiểu chữ

`SemanticValidationContextBuilder.AddFormatting()` hiện tìm run đầu khả dụng hoặc `p.Runs.FirstOrDefault()`; kết quả của run đầu **không đủ** để kết luận thuộc tính toàn trường. Điều kiện `effective_style_complete_span` trong các ứng viên là **cổng cần bổ sung bằng chứng**, chưa là capability đã verified. Phải xử lý style inheritance, explicit overrides, underline/bold/italic và các đoạn nhiều run; khi không đủ, trả `NOT_EVALUATED`/`NEEDS_REVIEW`.

### 3.3. Duplex, trang ngang và section

`SectionModel` hiện có `Margin*Twips`, kích thước/hướng trang và section boundary; không có cam kết nhận diện *mặt sau vật lý khi in hai mặt*. Các điều kiện `verified_print_side` và `confirmed_duplex_or_single_sided_intent` chỉ là **future evidence requirements**; không được hardcode `front_or_single` khi chỉ biết DOCX có khổ dọc. Đối với section landscape, các rule 09–12 không đánh giá. Phải kiểm và đo bằng cách dựng trang khi muốn chứng minh trang thực tế.

## 4. Đã sửa trong PR V16

- **02 tệp YAML lề trên/dưới:** bỏ điều kiện `document.page_side=front_or_single`, giữ `portrait`, nâng yêu cầu `verified_page_orientation`. Nguồn: II.IV.4.1–4.2 đều 20 mm.
- **02 tệp YAML lề trái/phải:** giữ điều kiện `front_or_single` nhưng thêm các capability giả định `verified_print_side` và `confirmed_duplex_or_single_sided_intent`. Không suy `front_or_single` từ `SectionModel` nếu không có chứng cứ.
- **01 script QA:** ngoài schema/value checks của V15, mô phỏng **ma trận applicability âm tính**: Party khác Administrative; Official Party khác specialized; dọc khác ngang; trước khác sau; không được coi thiếu parity/orientation/duplex evidence là đủ; formatting missing complete run/role evidence phải fail closed.
- Đây là phép **kiểm chứng tính nhất quán của metadata ứng viên**, không phải kiểm thử `RuleEvaluator` production hay sửa DOCX.

## 5. Phân loại bằng chứng và các nút chặn

| Cổng | Đã có | Còn thiếu |
|---|---|---|
| Văn bản gốc | Ảnh PDF p28, p31–32 và đối chiếu 12 giá trị | Đọc độc lập bởi reviewer thứ hai, phân xử `normativity` |
| YAML Schema | Draft 2020-12 + candidate values + unique IDs | Chính thức hóa target/property và role object trong hệ thống |
| Applicability | Ma trận V16 về duplex/landscape/regime, negative cases | Runtime context có regime/issuer/class; bằng chứng duplex/parity và renderer |
| Formatting | `EffectiveFormattingResolver` có khả năng reuse | Kiểm đủ run, style inheritance, mixed formatting |
| DOCX | Parser/section model và safeguard có từ dự án | Fixture minh chứng mặt sau, section landscape, bảng xen kẽ, nhiều run |
| Verification | `maturity=extracted`, `autofix=PROHIBITED`, không đổi release | Thẩm định pháp lý độc lập và kiểm thử runtime theo từng atom |

## 6. Điều kiện nghiệm thu

1. PR V16 chỉ merge dưới dạng **tài liệu/ứng viên chưa kích hoạt** khi 12 YAML và QA script có kết quả static CI PASS.
2. Không sửa trực tiếp schema, bộ ND30 435 rule, release manifest, bộ parser, hay workflow dùng chung đã khóa.
3. Không chuyển `extracted` thành `reviewed` chỉ vì một người có thể đọc ảnh PDF; phải có review độc lập về role, nguồn, pháp lý, ngoại lệ và chính sách tự sửa.
4. Không tuyên bố có `DOCX runtime PASS` khi chỉ chạy `verify_candidates.py` trên YAML.
5. Không tạo rule cho mặt sau theo suy đoán, hoặc tự động hoán đổi lề trên file người dùng chỉ dựa vào dự đoán kiểu in.

**Trạng thái cuối:** `V16_SCOPE_MODEL_REVIEWABLE`; `CANONICAL_VERIFIED=0`; `PARTY_RULE_RUNTIME=OFF`; `AUTOFIX=PROHIBITED`.
