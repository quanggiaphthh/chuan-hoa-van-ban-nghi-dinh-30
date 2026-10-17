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

## 6. Thẩm định bổ sung — tách kiểm tra lỗi khỏi quyền tự sửa (17/10/2026)

**Phân xử hiện hành:** Ma trận §3 bên trên thể hiện *khoảng trống tích hợp ở thời điểm audit*, không được đọc các nhãn `BLOCKED` như kết luận rằng quy định không thể kiểm tra. Riêng cột "trạng thái trước triển khai" có thể đồng thời trộn giới hạn validator, giới hạn tự sửa và yêu cầu dữ liệu văn thư. Bảng dưới đây là **bản phân xử chính thức cho hai trục độc lập**; khi có khác nhau, áp bảng §6.

- `CONDITIONAL`: phép kiểm **có thể thiết kế** với dữ liệu, evidence và capability nêu rõ, nhưng chưa xác nhận hệ thống hiện hành đã thực hiện.
- `REVIEW`: cần thẩm định nội dung/định danh/nguồn bên ngoài; không đưa ra `PASS` chỉ từ DOCX.
- `NOT_EVALUATED`: chưa có capability hoặc chứng cứ cần thiết; không được dùng làm `PASS`.
- `OFF`: không mở auto-fix trong PR này. `CANDIDATE_GUARDED` chỉ là phương án có thể xem xét sau này, **không phải** cấp phép sửa.

| ID | Nguồn chính (Hướng dẫn 05) | Khả năng validation (có điều kiện) | Chứng cứ tối thiểu | Quyền auto-fix (hiện tại) |
|---|---|---|---|---|
| R01 | PL2, TT1 | CONDITIONAL: cỡ 15 pt | Role tiêu đề + effective font size | CANDIDATE_GUARDED / OFF |
| R02 | II.I.1.2; PL1 ô 1 | CONDITIONAL: độ dài/nét đường kẻ | Render/layout + geometry nét liền | REVIEW_ONLY / OFF |
| R03 | II.I.2.2.1; PL1 ô 2 | CONDITIONAL: dấu sao trong phần đầu | Role cơ quan + anchor dấu sao | CANDIDATE_GUARDED / OFF |
| R04 | II.I.3.2.2 | CONDITIONAL: số dưới 10 có 0 | Văn bản có số được tách chính xác; đối chiếu sổ văn thư nếu kiểm tính hợp lệ | PROHIBITED sửa số đã cấp |
| R05 | II.I.3.2.2 | CONDITIONAL: dấu `-`, `/` đúng vai trò | Document type + issuing body + ký hiệu gốc | PROHIBITED sửa số/ký hiệu đã cấp |
| R06 | II.I.4.2.2 | CONDITIONAL: ngày dưới 10 có 0 | Issue-date role + token hóa ngày | SUGGEST_ONLY; không tự thay ngày ban hành |
| R07 | II.I.4.2.2 | CONDITIONAL: tháng dưới **3** có 0 | Issue-date role + token hóa tháng; giữ đúng ngưỡng nguồn | SUGGEST_ONLY; không tự thay tháng ban hành |
| R08 | PL2, TT5 | CONDITIONAL: tên loại 15–16 pt | Loại văn bản không phải công văn + effective formatting | CANDIDATE_GUARDED / OFF |
| R09 | PL2, TT5 | CONDITIONAL: trích yếu thường 14–15 pt, đứng đậm | Role trích yếu loại thường + style | CANDIDATE_GUARDED / OFF |
| R10 | PL2, TT5 | CONDITIONAL: trích yếu công văn 12 pt, nghiêng | Document type = công văn; PL1 ô 5b | CANDIDATE_GUARDED / OFF |
| R11 | II.I.5.2.1 | CONDITIONAL: đúng 5 dấu gạch nối | Subject/header ownership và ngoại lệ công văn | CANDIDATE_GUARDED / OFF |
| R12 | PL2, TT6 | CONDITIONAL: thân 14–15 pt | Phân vùng body bảo toàn và effective font | CANDIDATE_GUARDED / OFF |
| R13 | II.I.6.2.2 | CONDITIONAL: căn đều | Body role + effective paragraph alignment | CANDIDATE_GUARDED / OFF |
| R14 | II.I.6.2.2 | CONDITIONAL: thụt ~10 mm | Body role + first-line indent effective | CANDIDATE_GUARDED / OFF |
| R15 | II.I.6.2.2 | CONDITIONAL: spacing tối thiểu 6 pt | Body role + before/after spacing hiệu dụng | CANDIDATE_GUARDED / OFF |
| R16 | II.I.6.2.2 | CONDITIONAL: Exactly 18–22 pt | Body role + line spacing mode/value, không thay Multiple | CANDIDATE_GUARDED / OFF |
| R17 | II.I.6.2.3 | REVIEW: `-`, `;`, `.` của các căn cứ | Semantic legal-bases và nguồn nội dung | SUGGEST_ONLY; không tự viết lại căn cứ |
| R18 | II.I.9.2.3–4; PL1 | CONDITIONAL: `Kính gửi`/ `Kính trình` theo loại | Type + role nơi nhận + ô 5a/9b + render nếu cần | SUGGEST_ONLY / OFF |
| R19 | II.I.9.2.1–5; PL2 TT9 | CONDITIONAL: hai trường Nơi nhận và danh sách 14/12 pt | Recipients role riêng + punctuation + formatting | CANDIDATE_GUARDED / OFF |
| R20 | II.II.1 | CONDITIONAL: phụ lục, tiêu đề, chỉ dẫn, thứ tự | Appendix ownership + original block order + round-trip | SUGGEST_ONLY / OFF |
| R21 | II.IV.3 | CONDITIONAL: A4 hoặc ngoại lệ A5/mẫu in | Document class + section size + type applicability | CANDIDATE_GUARDED / OFF |
| R22 | II.IV.4.1 | CONDITIONAL: 4 lề độc lập | Section orientation + top/bottom/left/right in mm | CANDIDATE_GUARDED / OFF; tách 4 rule |
| R23 | II.IV.4.2 | REVIEW: mặt sau in hai mặt | Ý định duplex được xác nhận và section parity/render | SUGGEST_ONLY / OFF |
| R24 | II.IV.5.1; PL2 TT15 | CONDITIONAL: page field + ẩn trang đầu | Field semantics + header offsets + rendered pages | SUGGEST_ONLY / OFF |
| R25 | II.IV.5.2 | CONDITIONAL: số trang từng phụ lục riêng | Independent appendix page numbering + render | SUGGEST_ONLY / OFF |
| R26 | PL3 §1.1 | REVIEW: dấu bản sao giấy 80×50 mm | Bản giấy/ảnh quét đủ tỷ lệ và đo đạc | PROHIBITED tự tạo hoặc thay dấu |
| R27 | PL3 §1.1 | REVIEW: mực đỏ; tiêu đề hoa, đứng đậm 13 pt | Kiểm chứng vật mang tin/ảnh quét; riêng style cần nguồn số | PROHIBITED tự tạo hoặc thay dấu |
| R28 | PL3 §1.2 | REVIEW: `BS`, số từ 01 chung các hình thức sao/nhiệm kỳ | Sổ sao và cơ quan sao; chuỗi `BS` có thể check hình thức | PROHIBITED tự cấp hoặc đổi số |
| R29 | II.III.3.1–3.4; PL1 ô 14–15 | REVIEW: chữ ký số đúng vị trí và hợp lệ | PKI/certificate, signature container, chain/status; không chỉ icon | PROHIBITED sửa bản đã ký |

**Phân biệt hai khái niệm:** Dấu bản sao giấy (R26–R28) khác dấu sao ký tự `*` dưới tên cơ quan (R03); không dùng cùng parser role.

### 6.1. Kết quả kiểm toán canonical rules — không suy diễn từ metadata

- Trên `main` tại `078dae21`, `sources/registry.yaml` và `rule-packs/party-document-format-2026.yaml` ghi nhận nguồn Đảng nhưng không liệt kê canonical rule IDs; các thư mục `rules/` riêng cho Hướng dẫn 05 chưa được xác định trong cây Git của baseline.
- Lấy mẫu các file `v22-runtime/rules/generated-v24/{000,100,200,300,434}.yaml`: cả 5 đều có `regime: administrative`, `source_id: VN-STATE-ND30-2020`. **Chỉ đây là mẫu 5 tệp**, không phải kết luận kiểm đếm tất cả rule trong runtime.
- `RULE-MODEL.md` và `schemas/rule.schema.json` hỗ trợ maturity `draft/extracted/reviewed/verified`, nhưng metadata nguồn `implementation_status: core` **không chứng minh** tồn tại 29 nhóm HD05 được verified.
- Không tạo/đổi `maturity`, `source registry`, `rule pack` trong PR tài liệu này. Việc exact-dedup từng atomic rule **chưa hoàn thành** khi không có manifest đầy đủ canonical IDs và chứng cứ thực thi.

### 6.2. Các chốt kiểm chứng tối thiểu trước PR runtime

1. Với mỗi yêu cầu nguyên tử: một `source_locator`, đúng phạm vi/timestamp, đúng `target.property` và `constraint`, không trùng authority.
2. Với phép kiểm DOCX: chứng cứ block/part/section gốc, role đã xác nhận, effective style hoặc render nếu cần; khi thiếu trả `NOT_EVALUATED` hoặc `NEEDS_REVIEW`.
3. Với auto-fix: có policy theo maturity/capability, diff cục bộ, round-trip OOXML, xác nhận người dùng, không thay phần ngoài phạm vi; văn bản đã ký/mật/protected dừng tại policy gate.
4. Không gộp 29 nhóm tham chiếu thành 29 canonical rules. Nhiều nhóm phải tách nguyên tử: R18 (công văn/tờ trình), R19 (nhãn/danh sách nơi nhận), R22 (4 lề), R24 (vị trí/field/trang đầu), R27 (màu/kiểu/cỡ).
5. **Giữ nguyên 88 QA** có trong V12, chưa chạy QA mới và chưa xác nhận PASS; chỉ tạo test/fixture mới khi có behavioral gap được chứng minh.

**Quyết định độc lập:** `DOCS_REVIEW_GO` — ma trận kiểm toán có nguồn và điều kiện phân xử; `RUNTIME_AUTO_FIX_NO_GO` — chưa có canonical ID verification, DOCX capability evidence, runtime tests và round-trip.
