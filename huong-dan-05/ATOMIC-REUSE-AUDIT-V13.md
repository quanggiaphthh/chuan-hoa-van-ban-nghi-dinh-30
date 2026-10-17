# HD05 V13 — Kiểm toán nguyên tử và khả năng tái sử dụng runtime

**Ngày:** 17/10/2026  
**Đích:** PR #10 `docs/hd05-v12-audit-20261017`; baseline `main` `078dae21f9e23975676179fa189f5c01b605f49a`.  
**Trạng thái:** `DOCS_AUDITED / ATOMIC_CANDIDATES_ONLY / RUNTIME_NO_GO`.

> **Không tạo canonical rule trong tài liệu này.** Các mã `Rxx.yy` bên dưới là nhãn *vị trí thuộc tính ứng viên* để phục vụ phân rã, không phải ID chính thức đã được verified. Giá trị áp dụng, exact-dedup và capability kiểm chứng vẫn cần đóng trước triển khai.

## 1. Kết luận xác minh độc lập từ mã nguồn

| Quan sát | Bằng chứng file/symbol hiện hành | Kết luận REUSE FIRST |
|---|---|---|
| Release rule đang nạp được cấu hình cứng theo Nghị định 30 | `v22-runtime/src/LegalValidator/Catalog/RuleCatalog.cs`, `LoadVerifiedRelease()` trỏ tới `release/admin-nd30-verified-rc-v20.yaml` | **REUSE** RuleCatalog và evaluator; cần routing/release manifest hợp lệ trước khi dùng Party rules, không chép ND30 |
| Release này khai báo 435 ID bắt đầu `ND30.` | `v22-runtime/release/admin-nd30-verified-rc-v20.yaml`; đã đếm đúng 435 ID và 0 ID không bắt đầu `ND30.` | **MISSING** rule HD05 trong *release manifest được kiểm tra*; không khẳng định mọi rule ở nơi khác không tồn tại |
| Bộ phân loại hiện nay dùng quốc hiệu hành chính | `AdministrativeSemanticDetector.cs` / `NationalHeaderTexts` có `CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM`, `MottoTexts` có `ĐỘC LẬP - TỰ DO - HẠNH PHÚC` | **EXTEND theo contract** để nhận diện `ĐẢNG CỘNG SẢN VIỆT NAM`; tuyệt đối không thay hành vi nhận diện NĐ30 đã ổn định |
| Bộ đọc giữ đoạn và bảng trong hai collection riêng | `DocxParser.Parse()` duyệt `body.Elements<W.Paragraph>()` trước, rồi `body.Elements<W.Table>()`; `DocumentModel` không có `body_blocks_in_source_order[]` | **MISSING evidence** của thứ tự xen kẽ. Không dùng thứ tự `paragraphs[]`+`tables[]` để xác định chính xác ranh giới body/appendix |
| Cơ chế định dạng hiệu dụng đã tồn tại | `EffectiveFormattingResolver.Resolve()`, `FormattingValues`, `LineSpacingModel` | **REUSE**; phải đánh giá toàn bộ run/paragraph liên quan, không lấy một run đầu tiên làm kết quả đại diện |
| Adapter hiện lấy run đầu tiên có thể tìm được | `SemanticValidationContextBuilder.AddFormatting()` chọn run đầu từ `RunIds` hoặc `p.Runs.FirstOrDefault()` | **BLOCK** xác nhận định dạng toàn trường khi mixed-runs; dùng nguồn định dạng hiệu dụng nhưng cần chứng minh coverage |
| Kiểm tra và sửa DOCX hiện tại không là formatter pháp lý HD05 | `DocumentProcessorService.Profile = "generic-direct-alignment-spike"`; phương thức `ApplyAlignmentAsync` | **REUSE** khả năng inspect/mutation có giới hạn; không gán PASS cho `HD05` hoặc mở rộng thành full formatter mặc định |
| Bảo vệ gói DOCX và kiểm kê hash đã có | `PackageSafetyPreflight`, `SafeDocxSnapshot`, `PackagePreserver.PartSha256Inventory()` | **REUSE**; chữ ký số, tài liệu bảo vệ, chưa hỗ trợ, file nhạy cảm phải fail-closed |
| Rule evaluator bảo vệ trạng thái chưa đánh giá | `RuleEvaluator.Evaluate()` trả `NOT_EVALUATED` nếu thiếu capability, `NEEDS_REVIEW` với ambiguity | **REUSE** logic deterministic/status; không thay bằng AI tùy ý |
| Phạm vi PR là docs-only | `huong-dan-05/DAC-TA-CHUAN-V12.md`, `GAP-AUDIT-V12.md` và README | **PRESERVE PROVEN WORK**; không sửa schema, release pack, engine, tests đã PASS |

**Giới hạn bằng chứng:** Đây là static code audit và kiểm chứng nội dung release manifest, **không chạy .NET build/test, không chạy DOCX round-trip** trong lượt này.

## 2. Ma trận phân rã ứng viên — 59 thuộc tính từ 29 nhóm V12

Các trường ở cột *property slot* là **định danh tư duy kỹ thuật**, chưa được tạo trong schema runtime. `STYLE` = định dạng hiệu dụng; `ROLE` = vai trò/vị trí; `SECTION` = thông số section; `LAYOUT` = cần render/field; `STRUCTURE` = thứ tự khối/phụ lục; `EXTERNAL` = sổ văn thư/ý định nghiệp vụ; `PHYSICAL` = dấu trên giấy; `PKI` = kiểm chữ ký số; `SEMANTIC` = phán đoán nội dung.

| Atom ứng viên | Property slot (chưa phải target chính thức) | Nguồn (HD05) | Loại evidence | Validation |
|---|---|---|---|---|
| `R01.01` | heading.font_size_pt | PL2 TT1 | STYLE | Có điều kiện |
| `R02.01` | heading.underline_line_style | II.I.1.2; PL1 ô 1 | LAYOUT | Có điều kiện |
| `R02.02` | heading.underline_length_matches_text | II.I.1.2; PL1 ô 1 | LAYOUT | Có điều kiện |
| `R03.01` | issuer.separator_star_present | II.I.2.2.1 | ROLE | Có điều kiện |
| `R03.02` | issuer.separator_star_position | II.I.2.2.1 | ROLE | Có điều kiện |
| `R04.01` | document_number.leading_zero_under_ten | II.I.3.2.2 | EXTERNAL | Có điều kiện |
| `R05.01` | document_number.number_symbol_hyphen | II.I.3.2.2 | ROLE | Có điều kiện |
| `R05.02` | document_number.kind_authority_slash | II.I.3.2.2 | ROLE | Có điều kiện |
| `R05.03` | document_number.joint_agency_hyphen | II.I.3.2.2 | ROLE | Có điều kiện |
| `R06.01` | issue_date.day_leading_zero_under_ten | II.I.4.2.2 | ROLE | Có điều kiện |
| `R07.01` | issue_date.month_leading_zero_below_three | II.I.4.2.2 | ROLE | Có điều kiện |
| `R08.01` | document_type.font_size_pt | PL2 TT5 | STYLE | Có điều kiện |
| `R09.01` | named_subject.font_size_pt | PL2 TT5 | STYLE | Có điều kiện |
| `R09.02` | named_subject.bold | PL2 TT5 | STYLE | Có điều kiện |
| `R09.03` | named_subject.upright | PL2 TT5 | STYLE | Có điều kiện |
| `R10.01` | official_letter_subject.font_size_pt | PL2 TT5 | STYLE | Có điều kiện |
| `R10.02` | official_letter_subject.italic | PL2 TT5 | STYLE | Có điều kiện |
| `R11.01` | named_subject.separator_five_hyphens | II.I.5.2.1 | ROLE | Có điều kiện |
| `R12.01` | body.font_size_pt | PL2 TT6 | STYLE | Có điều kiện |
| `R13.01` | body.paragraph_justification | II.I.6.2.2 | STYLE | Có điều kiện |
| `R14.01` | body.first_line_indent_mm | II.I.6.2.2 | STYLE | Có điều kiện |
| `R15.01` | body.paragraph_spacing_min_pt | II.I.6.2.2 | STYLE | Có điều kiện |
| `R16.01` | body.line_spacing_rule_exact | II.I.6.2.2 | STYLE | Có điều kiện |
| `R16.02` | body.line_spacing_pt | II.I.6.2.2 | STYLE | Có điều kiện |
| `R17.01` | legal_basis.item_prefix_hyphen | II.I.6.2.3 | SEMANTIC | Cần thẩm định |
| `R17.02` | legal_basis.nonfinal_semicolon | II.I.6.2.3 | SEMANTIC | Cần thẩm định |
| `R17.03` | legal_basis.final_period | II.I.6.2.3 | SEMANTIC | Cần thẩm định |
| `R18.01` | official_letter.addressee_at_5a | II.I.9.2.3–4; PL1 | ROLE | Có điều kiện |
| `R18.02` | submission.addressee_at_9b | II.I.9.2.3–4; PL1 | ROLE | Có điều kiện |
| `R19.01` | recipient_label.font_size_pt | II.I.9.2; PL2 TT9 | STYLE | Có điều kiện |
| `R19.02` | recipient_label.underline | II.I.9.2; PL2 TT9 | STYLE | Có điều kiện |
| `R19.03` | recipient_list.font_size_pt | II.I.9.2; PL2 TT9 | STYLE | Có điều kiện |
| `R20.01` | appendix.title_present | II.II.1 | STRUCTURE | Có điều kiện |
| `R20.02` | appendix.attachment_reference_present | II.II.1 | STRUCTURE | Có điều kiện |
| `R20.03` | appendix.numbering_if_multiple | II.II.1 | STRUCTURE | Có điều kiện |
| `R21.01` | section.page_size_by_document_class | II.IV.3 | SECTION | Có điều kiện |
| `R22.01` | section.margin_top_mm | II.IV.4.1 | SECTION | Có điều kiện |
| `R22.02` | section.margin_bottom_mm | II.IV.4.1 | SECTION | Có điều kiện |
| `R22.03` | section.margin_left_mm | II.IV.4.1 | SECTION | Có điều kiện |
| `R22.04` | section.margin_right_mm | II.IV.4.1 | SECTION | Có điều kiện |
| `R23.01` | duplex.back_margin_left_mm | II.IV.4.2 | EXTERNAL | Cần thẩm định |
| `R23.02` | duplex.back_margin_right_mm | II.IV.4.2 | EXTERNAL | Cần thẩm định |
| `R24.01` | page_number.horizontal_center | II.IV.5.1; PL2 TT15 | LAYOUT | Có điều kiện |
| `R24.02` | page_number.top_offset_mm | II.IV.5.1; PL2 TT15 | LAYOUT | Có điều kiện |
| `R24.03` | page_number.first_page_hidden | II.IV.5.1; PL2 TT15 | LAYOUT | Có điều kiện |
| `R24.04` | page_number.font_size_pt | II.IV.5.1; PL2 TT15 | LAYOUT | Có điều kiện |
| `R25.01` | appendix.page_number_restart | II.IV.5.2 | LAYOUT | Có điều kiện |
| `R26.01` | copy_stamp.width_mm | PL3 §1.1 | PHYSICAL | Cần thẩm định |
| `R26.02` | copy_stamp.height_mm | PL3 §1.1 | PHYSICAL | Cần thẩm định |
| `R27.01` | copy_stamp.ink_red | PL3 §1.1 | PHYSICAL | Cần thẩm định |
| `R27.02` | copy_stamp.title_font_size_pt | PL3 §1.1 | PHYSICAL | Cần thẩm định |
| `R27.03` | copy_stamp.title_bold | PL3 §1.1 | PHYSICAL | Cần thẩm định |
| `R27.04` | copy_stamp.title_uppercase | PL3 §1.1 | PHYSICAL | Cần thẩm định |
| `R28.01` | copy_number.mark_bs | PL3 §1.2 | EXTERNAL | Cần thẩm định |
| `R28.02` | copy_number.leading_zero | PL3 §1.2 | EXTERNAL | Cần thẩm định |
| `R28.03` | copy_number.sequence_by_term | PL3 §1.2 | EXTERNAL | Cần thẩm định |
| `R29.01` | digital_copy.org_signature_present | II.III.3; PL1 ô 14–15 | PKI | Cần thẩm định |
| `R29.02` | digital_copy.signer_signature_present | II.III.3; PL1 ô 14–15 | PKI | Cần thẩm định |
| `R29.03` | digital_copy.signatures_cryptographically_valid | II.III.3; PL1 ô 14–15 | PKI | Cần thẩm định |

**Phân bố:** 0 nhóm có thể thiết kế phép kiểm **có điều kiện**, 0 nhóm **cần thẩm định bên ngoài**. Phân bố này là trạng thái *khả năng kiểm tra*, **không phải pass rate hoặc runtime coverage**.

## 3. Nguyên tắc quyết định triển khai

1. **Không sao chép canonical constraint** từ ND30 sang Party regime. Chỉ tái sử dụng parser/resolver/evaluator/safety theo symbol cụ thể; giá trị chuẩn phải có source locator HD05 hoặc Quy định 399 và đúng thời gian hiệu lực.
2. **Không tự suy đủ dữ liệu**: nếu phần đầu/giữa/phụ lục chưa tách được an toàn, hoặc có bảng, Drawing, textbox, tracked changes, AlternateContent, headers/footers phức tạp chưa chứng minh bảo toàn thì đánh giá `NOT_EVALUATED`/`NEEDS_REVIEW`, không tự sửa.
3. **Không tự cấp/sửa số** (R04, R05, R28), không tự thay ngày ban hành thực tế (R06–R07), con dấu, ký số (R26–R29), nội dung căn cứ (R17), hoặc thay bộ bố cục dựa riêng vào heuristic (R18/R20).
4. **Chỉ xét mở rule sau khi**: canonical ID không trùng; 1 nguồn + locator đã verified; applicability theo loại/cơ quan/ngày; role/field ownership có evidence; rule nguyên tử; đánh giá toàn run/paragraph/section; thử trên DOCX thật; mở lại, đối chiếu OOXML/quan hệ ảnh/bảng và phần không sửa.
5. **REUSE test hiện có**: xem `v22-runtime/tests/DocumentEngine.Tests`, `SemanticDetector.Tests`, `LegalValidator.Tests`; map 88 tình huống của V12 vào bộ test đã có; chỉ bổ sung fixture khi xuất hiện contract/behavior mới có chứng cứ.
6. **Không thay locked Core** chỉ để nhận diện Hướng dẫn 05; ưu tiên extension composition/cấu hình regime phù hợp trước. PR code tương lai phải tách khỏi PR docs và có bằng chứng kiểm thử theo blast radius.

## 4. Thứ tự đóng các nút chặn theo blast radius

| Ưu tiên | Núi chặn cụ thể | Lựa chọn tích hợp tối thiểu | Bằng chứng để đóng |
|---|---|---|---|
| P0 | Chưa có release Party verified và loader chọn release ND30 cố định | Thẩm định cơ chế chọn minimal party rule pack dựa trên hợp đồng hiện tại | Không nạp nhầm ND30; source locator/temporal/authority chính xác |
| P0 | Detector chưa nhận diện tiêu đề `ĐẢNG CỘNG SẢN VIỆT NAM` theo regime | Thử extension rule/type detector, không sửa hành vi `AdministrativeSemanticDetector` cũ | Mẫu Đảng/công văn/tờ trình + negative test hành chính |
| P0 | Thứ tự xen kẽ đoạn–bảng chưa được đại diện rõ trong `DocumentModel` | Reuse OpenXML child order, bổ sung metadata/provenance tối thiểu nếu contract cần | 1→1 ownership, bảo toàn thứ tự, không mất/trùng bảng/ảnh khi round-trip |
| P1 | Context adapter đang lấy run đầu tiên | Reuse `EffectiveFormattingResolver` trên mọi run của vùng thuộc tính | Đo đúng mixed-runs và style inheritance |
| P1 | Chưa có layout/PKI/vật mang tin | Đánh giá NO-GO theo từng rule; không xây engine chỉ để lấy thêm coverage | `NOT_EVALUATED`/review khi thiếu bằng chứng; không cấp PASS giả |
| P2 | 88 QA là đặc tả chưa chạy | Map tới tests/fixtures hiện có, dùng negative/positive cases có giới hạn | Build/test logs, before/after DOCX, SHA/inventory và review |

## 5. Trạng thái nghiệm thu

- **DOCS GO**: đã đối chiếu 29 nhóm / 59 atom ứng viên, mỗi slot mang nguồn locator cấp nhóm, chỉ định loại chứng cứ.
- **CANONICAL RULES NO-GO**: không có bằng chứng hệ thống đã import và xác nhận `verified` cho các atom Party.
- **DOCX AUTO-FIX NO-GO**: chưa có bằng chứng preservation và condition checks; tuyệt đối không merge các rule tự sửa trong PR này.
- **MAIN UNCHANGED**: chỉ cập nhật tài liệu trong nhánh Draft PR #10.

Nguồn: [Hướng dẫn 05-HD/VPTW gốc](https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf) và [Quy định 399-QĐ/TW](https://xaydungchinhsach.chinhphu.vn/quy-dinh-so-399-qd-tw-cua-ban-bi-thu-ve-the-loai-tham-quyen-ban-hanh-va-the-thuc-van-ban-cua-dang-119260119124508363.htm).
