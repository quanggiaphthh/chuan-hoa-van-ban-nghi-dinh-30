# ChuanhoaBang — Báo cáo nghiên cứu mở rộng và điều chỉnh đặc tả TQA-NR V17.2

**Ngày đối chiếu:** 29/10/2026  
**Phạm vi:** Chỉ bảng **nội dung** Word/DOCX, **Times New Roman 13–14 pt**; nghiên cứu/đặc tả, KHÔNG phát triển công cụ, KHÔNG sửa repository GitHub, KHÔNG bắt buộc render ảnh/PDF trong quy trình AI đánh giá.  
**Đối chiếu:** V15 nghiên cứu của `quanggiaphthh/ChuanhoaBang`; V16 (118 case NR01–NR118); TQA-NR V17; errata V17.1.

## 1. Phán quyết

**GIỮ kiến trúc cốt lõi V17.1, CẦN điều chỉnh V17.2 ở bốn điểm.**

1. **Mỗi khẳng định phải có loại bằng chứng đúng:** cấu trúc đã đọc, hiệu dụng sau resolve style, ước lượng font có phép đo, hay chỉ giả định minh họa. Không coi giả định width là kết quả đã đo.
2. **Thêm mô hình quan hệ dữ liệu dạng đồ thị:** trước khi chấm thẩm mỹ phải bảo toàn nhãn hàng/cột, tiêu đề nhiều tầng và ánh xạ giữa dữ liệu với ngữ nghĩa của nó.
3. **Kiểm tra rủi ro chiều cao do paragraph line spacing**, không chỉ `trHeight`. Font 14 pt có thể vẫn bị che nếu quy tắc `w:spacing` kiểu `exact` không phù hợp.
4. **Thiết kế QA chống thiên lệch của LLM-as-a-judge:** bộ ca tham khảo do AI tạo không tự động trở thành ground truth. Cần đối chứng có chủ đích, đảo thứ tự phương án và một tập nhãn độc lập để hiệu chỉnh.

**Không** khởi tạo subsystem, UI, engine đo chữ, render pipeline, test runner hoặc phần mềm Word mới trong repo này. Tài liệu mô tả đầu vào, ràng buộc, phép đánh giá và kết quả mong đợi để AI hoặc hệ thống khác áp dụng.

## 2. Nguồn mới và mức suy luận được phép

| # | Nguồn | Phát hiện từ nguồn | Ứng dụng hợp lệ | Không được suy rộng |
|---|---|---|---|---|
| 01 | *TabReX*, ACL 2026 | Đánh giá bảng không cần bảng chuẩn bằng các thuộc tính và đồ thị tri thức; có truy vết lỗi cấp ô | Thiết kế `table_semantic_graph`, kiểm tra tính toàn vẹn các quan hệ header–data | Bài này **không** đo vẻ đẹp bố cục Word |
| 02 | *TabXEval*, Findings ACL 2025 | Phân biệt căn chỉnh cấu trúc với đối chiếu nội dung/ngữ nghĩa; có perturbation và human annotations | Thiết kế cặp bảng đối chứng, lỗi cấu trúc và lý do sai | Không lấy điểm TabXEval làm điểm thẩm mỹ |
| 03 | *PReMISE*, EMNLP 2026 | Rubric chính là đặc tả phép đo; một rubric có thể ổn định nhưng kém kháng ví dụ đánh lừa | QA mức đặc hiệu, tính tin cậy, preference-fit và adversarial robustness của rubric | Không dùng trọng số/ngưỡng thực nghiệm bài báo cho Word |
| 04 | *Am I More Pointwise or Pairwise?*, Findings EMNLP 2026 | Thứ tự lựa chọn mức điểm và thứ tự tiêu chí có thể làm chệch kết quả LLM judge | Hoán vị thứ tự ứng viên/tiêu chí khi blind test | Không coi một lần đánh giá là ground truth |
| 05 | *The Geometry of LLM-as-Judge*, Findings EMNLP 2026 | Nhiều AI đồng ý với nhau vẫn có thể khác đánh giá của con người | Yêu cầu nhãn chuyên gia độc lập nếu muốn khẳng định thang chất lượng phù hợp cảm nhận thật | Không dùng self-consistency làm bằng chứng đủ |
| 06 | *Pair Difficulty Matters*, EMNLP 2026 | Kết quả đồng thuận tổng hợp che khác biệt giữa cặp rõ ràng và cặp sát nhau | Báo kết quả riêng cặp chênh lệch lớn, cặp khó phân biệt và cặp không xác định | Không bắt một tỷ lệ thắng cố định cho các cặp mơ hồ |
| 07 | *Vietnamese Words Are Not Constructed from Syllables*, AAAI 2025 | Tranh luận về giả định phân đoạn từ trong xử lý tiếng Việt | Xây dựng lớp cụm-nghĩa cần giữ theo ngữ cảnh; không coi mỗi space là ranh giới từ độc lập | Không kết luận một thuật toán phân đoạn duy nhất là 'chuẩn' cho layout |
| 08 | *Vietnamese NLP comprehensive review*, Expert Systems with Applications 2026 | Vấn đề phân đoạn, mơ hồ cú pháp và dữ liệu ít theo lĩnh vực | Phân loại bảo vệ mã/ngày/tên cơ quan/cụm nghiệp vụ với provenance | Không chuyển kết quả NLP thành vị trí xuống dòng chắc chắn của Word |
| 09 | Microsoft Learn — `rFonts` và Word implementation notes | Một run có thể chọn font khác nhau theo nhóm ký tự; theme/style ảnh hưởng font hiệu dụng | Nâng cấp M01: xác minh `ascii/hAnsi/eastAsia/cs`, theme, `sz/szCs` theo ký tự | Chỉ thấy `w:ascii="Times New Roman"` không đủ chứng minh toàn bộ chữ dùng TNR |
| 10 | Microsoft Learn — `w:spacing` / `lineRule` | `auto` đo theo 240 phần dòng; `exact`/`atLeast` dùng twips; `exact` quá nhỏ có thể che chữ | Bổ sung `PARAGRAPH_LINE_BOX_RISK` trước kết luận dễ đọc | Không suy pixel clipping thực tế nếu chưa có bộ dàn trang Word |
| 11 | Microsoft Learn — `tblW`, `tcW` | Độ rộng đều là ưu tiên, có thể bị thay đổi vì xung đột nội dung/grid | Tách hình học khai báo, ràng buộc khả thi, bất định layout | Không gắn `FIT_CONFIRMED` chỉ vì cộng width hợp lệ |
| 12 | Section508.gov — Word Accessibility for Document Designers, **v1.0.2 (10/2026)** | Khóa tài liệu cập nhật về tiếp cận Word bao gồm bảng | Xem là nguồn tham khảo bổ sung cho quan hệ tiêu đề–dữ liệu | Section 508 không phải luật bắt buộc trực tiếp cho NĐ30/HD05 Việt Nam |

## 3. Kiểm tra trực tiếp 100 case V16 đã có

Đã đọc `ChuanhoaBang_V16_100_Case_Structured.jsonl` trong dữ liệu nghiên cứu được tạo từ cuộc trao đổi này; thống kê theo chính trường JSONL (không kiểm tra DOCX gốc, không render).

| Trường | Kết quả | Hệ quả nghiên cứu |
|---|---:|---|
| Tổng case NR19–NR118 | 100 | Là case synthetic, chưa có khảo sát người đọc |
| `case_status=SYNTHETIC_SPEC_NOT_EXECUTED` | 100 | Không thể diễn giải là 100 ca đã PASS trong Word |
| `width_provenance=ILLUSTRATIVE_VECTOR_NOT_FONT_MEASURED` | 64 | 64 vector là số minh họa, **chưa** phải độ rộng TNR đã đo |
| `width_provenance=NO_VECTOR_DEFINED` | 36 | Không có vector width để kiểm tra chất lượng hình học theo ứng viên |
| `evidence=TEXT_ESTIMATED` | 66 | **Rủi ro diễn giải:** nhãn thiếu phân biệt phép đo thực, dự đoán heuristic và phép ví dụ |
| `font_pt=14` | 98 | Thiếu đa dạng nếu dùng để kiểm định lựa chọn giữa 13–14 pt |
| `font_pt=13` | 2 | Cần thêm đối chứng 13 pt **chỉ khi có quyền dùng cỡ này** |
| `decision=DESIGN_RECOMMENDED` | 62 | Quyết định đặc tả, chưa có đối chứng independent |
| `decision=REVIEW_REQUIRED` | 21 | Đã có tình huống bất định, cần bảo tồn |
| `decision=PRESERVE` | 9 | Đã có ngoại lệ biểu mẫu/mẫu cố định |
| `decision=KEEP_EXISTING` | 7 | Đã có ứng viên no-op |
| `decision=NO_SAFE_FIT` | 1 | Mới có một ca vô nghiệm, dễ thiếu coverage |

**Nhận định:** 66 nhãn `TEXT_ESTIMATED` không nhất thiết sai trong mọi ngữ cảnh nếu tác giả dùng nó với nghĩa 'ước lượng logic'. Nhưng khi đồng thời có `ILLUSTRATIVE_VECTOR_NOT_FONT_MEASURED`, người đọc máy rất dễ hiểu nhầm đã thực hiện phép đo font. Cần sửa hợp đồng dữ liệu và định nghĩa nhãn, không tự tuyên bố 66 ca sai định dạng.

**Chỉnh sửa nên làm ở cấp đặc tả:**

- `width_provenance`: `ILLUSTRATIVE` | `FONT_METRIC_MEASURED` | `FORM_LOCKED` | `NONE`.
- `evidence_kind`: `SOURCE_XML` | `RESOLVED_STYLE` | `FONT_METRIC_ESTIMATE` | `HEURISTIC_ASSUMPTION` | `HUMAN_LABEL` | `UNKNOWN`.
- `metric_value`: số kèm đơn vị, độ bao phủ, phương pháp, provenance; **không có phép đo thì không gắn loại `FONT_METRIC_ESTIMATE`**.
- `answer_status`: `SPEC_EXPECTED` | `STRUCTURALLY_CHECKED` | `EXPERT_ADJUDICATED` — dữ liệu hiện có phần lớn ở `SPEC_EXPECTED`.
- `font_pt_policy`: `14_MANDATORY` | `13_14_BOTH_ALLOWED` | `13_ALLOWED` | `UNKNOWN` để tránh tự ép 13 pt khi mẫu/quy định yêu cầu 14 pt.

Không thay đổi các case cũ trong lần nghiên cứu này.

## 4. V17.2 — Cấu trúc đánh giá được hiệu chỉnh

### 4.1. Giữ M01–M12, bổ sung hai cờ rủi ro ngoài điểm thẩm mỹ

| Cờ | Dữ liệu | Đầu ra | Lý do |
|---|---|---|---|
| `R13_PARAGRAPH_LINE_BOX` | `w:pPr/w:spacing`, `w:lineRule`, đoạn/paragraph style kế thừa; cỡ TNR 13/14; trước/sau đoạn | `CLEAR_BY_RULE` / `RISK_ESTIMATED` / `UNKNOWN` | Cùng một `trHeight=auto`, `lineRule=exact` quá nhỏ vẫn có nguy cơ che chữ. `exact` được so trong đơn vị pt; `auto` là bội số dòng. |
| `R14_SEMANTIC_ASSOCIATION` | Topology thực tế, hàng tiêu đề, cột định danh, spans, ngữ nghĩa hàng–cột | `UNAMBIGUOUS` / `AMBIGUOUS` / `UNKNOWN` | Dữ liệu dễ đọc khi có thể nhận ra mỗi giá trị thuộc chỉ tiêu, đơn vị, thời kỳ và đối tượng nào. |

Đây là **risk flags**, không ép vào trung bình 0–100. M01–M12 giữ tên, tránh tạo hai hệ metric song song, nhưng M01/M07/M12 phải dẫn tới R13/R14 khi phù hợp.

### 4.2. Mô hình quan hệ cấu trúc–ngữ nghĩa

`Table Semantic Graph (TSG)` là **đặc tả biểu diễn**, không phải yêu cầu tạo ứng dụng đồ thị:

- Node: `TABLE`, `HEADER_GROUP`, `COLUMN_HEADER`, `ROW_LABEL`, `DATA_CELL`, `TOTAL`, `NOTE`, `UNIT`, `PERIOD`.
- Edge: `SPANS_GRID`, `LABELS`, `BELONGS_TO_GROUP`, `HAS_UNIT`, `HAS_PERIOD`, `AGGREGATES`, `REFERS_TO`.
- Mỗi edge có `source_cell`/vị trí, mức nguồn: xác định bằng cấu trúc, người dùng cung cấp, suy luận theo quy tắc, hoặc mơ hồ.
- Không có edge chắc chắn không tự động là lỗi; tuy nhiên quan hệ thiết yếu không thể suy ra phải báo `REVIEW_REQUIRED`.
- Với biểu mẫu bắt buộc, áp `PRESERVE` thay vì tái cấu trúc chỉ để giảm độ phức tạp.

Ví dụ: bảng gồm `Năm` → `Kế hoạch`, `Thực hiện` cho 2025/2026. Một ô `83` phải gắn được với **năm 2026**, nhãn **Thực hiện** và đơn vị đã khai báo. Nếu ô gộp gây hai cách diễn giải, rủi ro không được bù bởi bảng có viền rất đẹp.

### 4.3. Công thức đánh giá không render theo thứ tự ưu tiên

Đầu vào `X` phải gồm: snapshot OOXML, effective style, profile pháp lý/mẫu, section/container, table role, reader task, bảo vệ nội dung và trạng thái nguồn đo font TNR. Không đủ thì `UNKNOWN`/`REVIEW`, không tự bịa chiều rộng.

```text
G0: Authority / integrity / locks / effective TNR font 13–14 pt
G1: Semantic association, critical tokens, table topology
G2: Width feasibility, row/paragraph line-box risks and evidence uncertainty
G3: Task-conditioned readability: look-up, compare, action, compliance, summary
G4: Aesthetic coherence: padding, alignment, hierarchy, borders, family consistency
G5: Minimal edit / baseline preferred when benefits are weak
```

*G0 là hard gate. G1–G2 phải phân biệt lỗi xác minh được với rủi ro ước lượng.* Không dùng G3/G4 để che điều kiện G0 sai.

**Hình học:** `W_usable` tính từ đúng section và container; `w:tblW`/`w:tcW` là preferred widths; `sum(w_grid) <= W_usable` là kiểm tra hình học đề xuất khi ngữ cảnh xác định được, **không** phải lời xác nhận bố cục thực Word.

**Đo chữ:** phải dùng face TNR thực, 13 hoặc 14 pt, Unicode/shaping có ghi phiên bản; xác định grapheme, break opportunity và các cụm giữ nguyên theo nghiệp vụ. Trả `M1_MODEL_ESTIMATE` nếu thực sự đo font, còn ví dụ tỷ lệ cột không đo phải dùng `ILLUSTRATIVE_ONLY`.

**Chiều cao:** với hàng thường, ước lượng chiều cao từ tổng dòng/paragraph, `w:spacing`/`lineRule`, before/after, padding rồi lấy phần tử chi phối chiều cao trong hàng. Riêng vertical merge, nested table, field, drawing, exact height bất thường cần chính sách ngoại lệ.

### 4.4. Ghi chép trạng thái kết luận

```yaml
case_id: NRxx
source_authority: ND30 | HD05 | OFFICIAL_FORM | UNKNOWN
font_policy: TNR_14_ONLY | TNR_13_OR_14 | UNKNOWN
font_actual: RESOLVED | UNRESOLVED
font_metrics: MEASURED | NOT_MEASURED
geometry: DECLARED | CONSTRAINED | UNKNOWN
semantic_graph: UNAMBIGUOUS | AMBIGUOUS | UNKNOWN
risk_flags: [R13_PARAGRAPH_LINE_BOX, R14_SEMANTIC_ASSOCIATION]
proposal_status: KEEP_EXISTING | DESIGN_RECOMMENDED | REVIEW_REQUIRED | PRESERVE | NO_SAFE_FIT
evidence_class: SOURCE_XML | RESOLVED_STYLE | FONT_METRIC_ESTIMATE | HEURISTIC_ASSUMPTION
render_verification: NOT_PERFORMED
ground_truth_status: SPEC_EXPECTED | EXPERT_ADJUDICATED
reason_codes: []
```

Trường trên là **mẫu khai báo minh họa** để AI đọc và xuất giải thích; không phải API, schema đã triển khai hay tệp phải tạo trong repo.

## 5. Năm kiểu đối chứng có giá trị hơn việc tăng số case thô

| Cặp | Hai phương án chỉ khác gì? | Kỳ vọng có thể đánh giá không render |
|---|---|---|
| C1 | Cả hai dùng TNR14, nhưng `w:spacing` `auto` so với `exact` thấp | Phương án `exact` bị gắn `R13`, không tuyên bố clipping đã quan sát |
| C2 | Cả hai khai báo TNR14 ở run ASCII, một bản `hAnsi` dùng font khác | Bản thứ hai không được xác nhận đạt **font TNR toàn bộ nội dung** nếu có ký tự thuộc vùng khác |
| C3 | Cùng kích thước, một bảng header nhiều tầng giữ nhãn năm/đơn vị, một bảng mất liên kết nhãn | Bản thứ hai có `R14`; không chấm thắng chỉ nhờ đường viền |
| C4 | Cùng bảng thống kê, bản A có nhãn/đơn vị rõ, bản B dùng màu để thay nhãn | Bản B bị phạt rủi ro quan hệ dữ liệu/khả năng tiếp cận, dù màu dễ nhìn |
| C5 | Hai cách phân bổ width đều hợp lệ; một bên nới cột mô tả nhưng làm cột ngày ngắt | Ưu tiên phương án bảo vệ token; nếu chưa đo glyph thì `REVIEW`, không ép chọn |

**Các cặp khác cần bổ sung:** mô hình tiếng Việt (tên cơ quan dài, nhiều âm tiết), 13↔14 pt chỉ nơi cho phép; `gridSpan` hợp lệ vs mơ hồ; `tblLook` đảo kiểu header; bộ cột số có số âm, số 0 đầu; `NO_OP_VALID`; bảng ngắn hẹp hợp lý; `pPr` multiple paragraphs với before/after khác nhau; spacing auto/exact/atLeast.

## 6. Blind-test và hiệu chỉnh: kết luận nghiên cứu, không triển khai

1. **Chia lỗi theo độ khó:** *obvious-hard* (font ngoài phạm vi, mất token, sai chiều rộng không khả thi), *task-dependent* (nới cột nội dung vs ngày), *close-call* (hai ứng viên tương tự). Báo kết quả riêng, không chỉ tỉ lệ tổng.
2. **Đảo thứ tự A/B và thứ tự tiêu chí** trong lần thẩm định độc lập; nếu kết luận đổi không có lý do, ghi `JUDGE_ORDER_UNSTABLE`.
3. **Giữ tập holdout** khác case dùng khi viết rubric; giảm nguy cơ AI nhớ answer key.
4. **Có nhãn độc lập của người có chuyên môn** đối với một tập bảng thực tế nếu nghiên cứu muốn khẳng định khả năng dự đoán cảm nhận “đẹp, dễ đọc”. Có thể dùng đánh giá hiện hữu của chuyên gia trong tập nghiên cứu; không phải thêm bước render khi AI xử lý tài liệu người dùng.
5. **Hiệu chỉnh ngưỡng bằng dữ liệu thật** thay vì tự ấn định `Q>=80`, `fragment_rate<10%` hay tỷ lệ chia cột chung. Với cặp gần tương đương hoặc rủi ro bất định, `KEEP_EXISTING`/`REVIEW_REQUIRED` là đáp án hợp lệ.
6. **Đánh giá QA adversarial**: trường hợp số 01, thay dấu thập phân, nhãn hai tầng, thông tin bị thiếu nhưng bảng vẫn nhìn đẹp, TNR chỉ tồn tại ở thuộc tính ASCII, dòng `exact` thấp, 14pt ghi ngoài nhưng 12pt kế thừa ở nơi khác.

## 7. Kế hoạch đề xuất — chỉ thực hiện tài liệu đặc tả

| Thứ tự | Hạng mục | Kết quả bàn giao | Định nghĩa hoàn thành |
|---|---|---|---|
| P0 | Errata chuẩn hóa provenance cho 118 case | `CASE_EVIDENCE_ERRATA_V17_2.md` | Không dùng nhãn font-measured khi chưa có measurement; mọi giá trị gắn nguồn |
| P0 | Bổ sung rủi ro khoảng dòng đoạn/Word style | Một phụ lục cho M01/M07/R13 | Có 13–14 pt, hiệu dụng fonts, spacing `auto/exact/atLeast`, bài toán 14pt nhưng line-box nhỏ |
| P1 | Đặc tả quan hệ header–data dạng TSG | Phụ lục M12/R14 | Bảng nhiều tầng/gộp hợp lệ/khó hiểu đều có reason codes, không mặc định sai |
| P1 | Rubric đối chứng có cơ chế chống judge bias | 30–40 cặp + đáp án độc lập theo kịch bản | A/B đảo thứ tự không thay đổi kết luận cứng; có `TIE`/`UNKNOWN` |
| P2 | Đối chiếu 118 case vào M01–M12 và R13/R14 | Ma trận nguồn–metric–rủi ro–mức chứng cứ | 100% case có loại bằng chứng; không dựng số đo chưa có |
| P2 | Đối chiếu nghiên cứu với nhãn độc lập, nếu được cung cấp | Báo cáo độ phù hợp theo nhóm lỗi | Không công bố accuracy khi chưa có nhãn độc lập |

**Điều không cần làm:** tăng thêm 100 ca tương tự; phát triển package, CLI, Word macro, plugin hoặc thư viện font; kiểm tra PDF/render mỗi bảng; đưa bảng Word thành HTML; đào tạo/fine-tune mô hình mới.

## 8. Các nguồn tham khảo chính mới hoặc có ý nghĩa mới trong đợt này

1. Anvekar et al., **TabReX: Tabular Referenceless eXplainable Evaluation**, ACL 2026: https://aclanthology.org/2026.acl-long.2176/
2. **TabXEval: Why this is a Bad Table? An eXhaustive Rubric for Table Evaluation**, Findings ACL 2025: https://aclanthology.org/2025.findings-acl.1176/
3. Roy et al., **PReMISE: Policy Rubrics as Measurement Specifications for LLM Judges**, EMNLP 2026: https://aclanthology.org/2026.emnlp-main.2653/
4. Xu et al., **Am I More Pointwise or Pairwise? Revealing Position Bias in Rubric-Based LLM-as-a-Judge**, Findings EMNLP 2026: https://aclanthology.org/2026.findings-emnlp.1444/
5. Mukherjee et al., **The Geometry of LLM-as-Judge: Why Inter-LLM Consensus Is Not Human Alignment**, Findings EMNLP 2026: https://aclanthology.org/2026.findings-emnlp.1696/
6. Brocai & Becker, **Pair Difficulty Matters**, EMNLP 2026: https://aclanthology.org/2026.emnlp-main.2676/
7. Nguyen et al., **Vietnamese Words Are Not Constructed from Syllables**, AAAI 2025: https://ojs.aaai.org/index.php/AAAI/article/view/34581
8. **Natural language processing and computational linguistics for Vietnamese: A comprehensive review**, Expert Systems with Applications 2026: https://www.sciencedirect.com/science/article/pii/S095741742601609X
9. Microsoft, **RunFonts Class (`rFonts`)**: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.runfonts?view=openxml-3.0.1
10. Microsoft, **Word implementation notes for `rFonts`**: https://learn.microsoft.com/en-us/openspecs/office_standards/ms-oi29500/aef3c9a6-5d6c-434b-90b7-85e761fd8e62
11. Microsoft, **SpacingBetweenLines.Line Property**: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.spacingbetweenlines.line?view=openxml-3.0.1
12. Microsoft, **TableWidth Class (`tblW`)**: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.tablewidth?view=openxml-3.0.1
13. Microsoft, **TableCellWidth Class (`tcW`)**: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.tablecellwidth?view=openxml-2.20.0
14. Microsoft, **Conditional table style formatting precedence**: https://learn.microsoft.com/en-us/openspecs/office_standards/ms-oi29500/2ac331d4-cf1e-4fa0-8bca-6da74411e284
15. Section508.gov, **Microsoft Word Accessibility for Document Designers**, v1.0.2 10/2026: https://www.section508.gov/training/courses/microsoft-word-accessibility-designers/

## 9. Kết luận cuối

**Không thay kiến trúc V17.1; bổ sung errata V17.2 để chặn 'bằng chứng giả', xử lý line spacing độc lập với row height, bảo toàn quan hệ ngữ nghĩa và chống thiên lệch thẩm định của AI.** Đối với Times New Roman 13–14 pt, đây là các điều chỉnh thiết thực hơn việc tiếp tục mở rộng số case mà chưa hiệu chỉnh thang đánh giá.

*Trạng thái: báo cáo nghiên cứu và audit dữ liệu cấu trúc; chưa thực hiện font shaping thật, chưa đánh giá bằng Word, chưa thu nhãn chuyên gia, chưa chỉnh sửa repository.*
