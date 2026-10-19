# P0 READINESS V14 — Ba điểm nghẽn trước triển khai Hướng dẫn 05

**Ngày kiểm toán:** 17/10/2026  
**Repository:** `quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30`  
**Baseline:** `078dae21f9e23975676179fa189f5c01b605f49a`  
**PR tài liệu:** #10 (`docs/hd05-v12-audit-20261017`)  
**Quyết định:** `DOCS_GO / CODE_NO_GO / AUTO_FIX_NO_GO`.

## 1. Mục tiêu và nguồn bằng chứng

Hoàn tất research-before-build để xác định cách **tái sử dụng tối đa** parser/semantic detector/rule evaluator sẵn có, nhằm chuẩn bị tích hợp Hướng dẫn 05-HD/VPTW mà **không phá vỡ Nghị định 30**.

Căn cứ nghiệp vụ: [Hướng dẫn 05-HD/VPTW (PDF chính thức, 36 trang)](https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf), [Quy định 399-QĐ/TW](https://xaydungchinhsach.chinhphu.vn/quy-dinh-so-399-qd-tw-cua-ban-bi-thu-ve-the-loai-tham-quyen-ban-hanh-va-the-thuc-van-ban-cua-dang-119260119124508363.htm).

Căn cứ kỹ thuật chính thức: [Microsoft Learn — Structure of a WordprocessingML document](https://learn.microsoft.com/en-us/office/open-xml/word/structure-of-a-wordprocessingml-document), [Microsoft Learn — Open XML SDK](https://learn.microsoft.com/en-us/office/open-xml/about-the-open-xml-sdk). Theo cấu trúc WordprocessingML, `w:body` là container cho các block-level element (`w:p`, `w:tbl`, ...), vì vậy thứ tự gốc phải được quan sát theo chuỗi child elements trong XML thay vì ghép hai tập riêng.

Đối tượng chưa nằm trong phạm vi PR: thay code, tạo rule `verified`, thay behavior UI/host, migration, chỉnh DOCX đã ký, commit lên `main`.

## 2. P0-A — Chọn bộ quy tắc theo regime và bản release

| Câu hỏi | Bằng chứng đã đọc |
|---|---|
| Đã có RuleCatalog và RuleEvaluator chưa? | Có, `v22-runtime/src/LegalValidator/Catalog/RuleCatalog.cs` và `Evaluation/RuleEvaluator.cs` |
| Loader hỗ trợ lựa chọn Party manifest chưa? | Chưa thấy. `LoadVerifiedRelease(root)` hiện gắn cứng `release/admin-nd30-verified-rc-v20.yaml` |
| Release hiện hành chứa gì? | `v22-runtime/release/admin-nd30-verified-rc-v20.yaml`: 435 ID, tất cả bắt đầu `ND30.` và `regime: administrative` |
| Source Registry/Party rule pack đủ chứng minh chạy được chưa? | Chưa. `sources/registry.yaml` + `rule-packs/party-document-format-2026.yaml` chỉ đăng ký nguồn/selectors, không đồng nhất với verified runtime manifest |

**Reuse-first integration (chưa triển khai):** duy trì `RuleCatalog` là authority duy nhất. Khi đã có ít nhất một canonical rule Party được phê duyệt, mở *đường chọn manifest an toàn* ở boundary loader thay vì tạo rule engine mới. Cơ chế đang nạp ND30 **phải giữ nguyên mặc định**; Party/unknown không được lặng lẽ fallback sang ND30.

**Điều kiện triển khai:** (a) có manifest Party độc lập chứa chỉ ID đã verified; (b) explicit regime + issuer + issue date + source locator; (c) validator từ chối rule không đúng regime hoặc thiếu capability; (d) signature/authority/safety vẫn giữ behavior cũ; (e) không nạp 435 rule ND30 cho Party theo mặc định.

**Test tối thiểu có ý nghĩa**: ND30 default nạp đúng 435 ID không đổi; Party không có manifest báo không khả dụng, không PASS giả; Party có manifest chưa verified phải bị từ chối; unknown regime không auto-fix; temporal resolver xử lý 09/01/2026 và 27/05/2026 độc lập.

**Tệp có thể ảnh hưởng (không sửa trong PR #10):** `RuleCatalog.cs`, phần adapter regime/loader thực sự được dùng, manifest Party verified **chỉ sau khi nguồn/rule được kiểm chứng**, `LegalValidator.Tests`.

## 3. P0-B — Nhận diện ngữ nghĩa cho văn bản của Đảng

| Khả năng hiện có | Kết luận |
|---|---|
| `AdministrativeSemanticDetector.Detect(DocumentModel)` | **REUSE** source-evidence, classification confidence, ambiguity và deterministic regex primitives |
| `NationalHeaderTexts` | Chỉ `CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM`; không được thay bằng tiêu đề Đảng trên cùng đường mặc định |
| `MottoTexts` | Chỉ `ĐỘC LẬP - TỰ DO - HẠNH PHÚC` |
| `SemanticRole` | Có nhiều role hữu dụng (issuer, number, recipient, appendix, copy), chưa có dedicated `PartyHeading` |
| `AdministrativeDocumentClass` | Các class: OfficialLetter, NamedAdministrativeDocument, Appendix, Copy, Unknown; **chưa đủ** phân loại 25 thể loại + 8 giấy tờ Đảng |

**Thiết kế tối thiểu:** xác định regime trước semantic detection. Giữ bộ detector hành chính không đổi; ưu tiên composition từ `DocumentModel`, `SemanticComponentCandidate`, `SemanticSourceEvidence` để tạo Party-specific profile/adapter. **Không gắn nhãn quốc hiệu hành chính cho tiêu đề Đảng** và không nhồi 25+8 thành enum hành chính. Chỉ mở rộng contract nếu tìm thấy thiếu hụt được tái hiện bằng fixture. Gán `front/body/end/appendices[]` là phân vùng kỹ thuật, không phải điều khoản quy định.

**Test trực tiếp:** (i) tiêu đề ĐẢNG CỘNG SẢN VIỆT NAM ở đầu; (ii) mẫu công văn có `Kính gửi` tại 5a; (iii) mẫu tờ trình có `Kính trình` theo 9b; (iv) Nơi nhận cuối; (v) ký nhận nhiều người; (vi) appendix; (vii) biên bản; (viii) ảnh/chữ ký/con dấu là object chưa chắc chắn → review; (ix) negative ND30 vẫn phân loại như trước.

**Tệp tham chiếu:** `v22-runtime/src/SemanticDetector/Detection/AdministrativeSemanticDetector.cs`, `Model/SemanticModels.cs`, `src/LegalValidator/Adapters/SemanticValidationContextBuilder.cs`, `tests/SemanticDetector.Tests/SemanticDetectorTests.cs`. Tránh sửa detector hiện có nếu adapter có thể composition.

## 4. P0-C — Bảo toàn thứ tự xen kẽ đoạn–bảng và provenance

**Phát hiện có thể tái hiện từ source:**

- `DocxParser.Parse()` duyệt `body.Elements<W.Paragraph>()` và nạp `Paragraphs` **trước**, rồi `body.Elements<W.Table>()` thành `Tables`. Đây không phải thứ tự `w:p/w:tbl` xen kẽ.
- `DocumentModel` ghi `Paragraphs` và `Tables` là hai collections riêng, không có thứ tự block thống nhất/anchor rõ ràng.
- `AdministrativeSemanticDetector.Flatten()` duyệt các bảng trước rồi mới đến `d.Paragraphs`; `SemanticSourceEvidence.Order` theo thứ tự flatten đó, **không chứng minh thứ tự nguồn XML**.
- Hàm `P(W.Paragraph)` lấy `p.Elements<W.Run>()` trực tiếp, không tự chứng minh bảo toàn text nằm trong tracked changes/SDT/textbox/nested objects. `T(W.Table)` đọc paragraph trực tiếp của mỗi cell; không tự chứng minh nested tables.
- `PackagePreserver.PartSha256Inventory()` là primitive hữu ích để so trước/sau, **không tự khắc phục mất thứ tự semantic**.

**Khuyến nghị thiết kế tối thiểu:** tái sử dụng chính SDK `DocumentFormat.OpenXml` đã có, đi theo `body.ChildElements` trong đúng thứ tự nguồn, sinh ánh xạ `ordered-block-ref {source-part, source-path/id, order, kind}`. Không cần thư viện mới. Nếu block không biểu diễn đầy đủ (textbox, nested table, SDT, `AlternateContent`, drawing, field...) phải giữ unclassified/provenance và ngăn structural auto-fix.

**Kiểm tra bắt buộc:** fixture chứa thứ tự `p1, tbl1, p2, tbl2, p3`; yêu cầu 5 block duy nhất, đúng thứ tự. Fixture khác có `p1, table(cell paragraph), p2`; không nhân đôi cell paragraph như top-level block. Kiểm tra appendix sau bảng; header/footer không bị nhầm top-level body; `AlternateContent` không mất hoặc bị tự chọn nhầm một nhánh. Sau đó copy/round-trip, reopen, đối chiếu part inventory, rels, table/cell count, image refs và mọi phần ngoài phạm vi sửa.

**Điểm tương thích:** giữ các public properties cũ `Paragraphs`/`Tables` nếu các consumer/tests hiện có phụ thuộc; metadata order nên là extension/additive contract đã xác minh. Không sửa `PackagePreserver` nếu không chứng minh lỗi.

**Tệp ảnh hưởng dự kiến:** `src/DocumentEngine/Parsing/DocxParser.cs`, `Model/Models.cs`, `src/SemanticDetector/Detection/AdministrativeSemanticDetector.cs` hoặc source-order adapter, và test có ảnh hưởng trực tiếp.

## 5. Nút chặn bổ sung được xác định trong lúc audit

| Vấn đề | Tệp / symbol | Quyết định |
|---|---|---|
| Mixed formatting: adapter chọn run đầu có thể tìm | `SemanticValidationContextBuilder.AddFormatting()` | **P1**: không kết luận toàn trường từ run đầu; thử multi-run, inheritance, override trước |
| `DocumentProcessorService.Profile` | `generic-direct-alignment-spike` | Không coi formatter NĐ30 hoặc HD05 production; chỉ reuse giới hạn |
| `RuleEvaluator.Evaluate()` | `NOT_EVALUATED` nếu thiếu runtime capability | REUSE, không thay bằng AI |
| Baseline test fixtures | `v22-runtime/fixtures/docx/`, `fixtures/v23/` | Có sẵn 22 fixture DOCX + 1 fixture DOCM ở v22, và 11 fixture DOCX ở v23; ưu tiên reuse và bổ sung tối thiểu loại Party/interleaving |
| CI workflows v22/v23/v24 | trigger chủ yếu `workflow_dispatch` và `push` ở feature branches cố định | **Không chứng minh CI tự chạy cho Draft PR #10**; code PR tương lai phải có gate tương ứng hoặc chạy lệnh thực tế |
| Môi trường audit hiện tại | không có dotnet SDK; không clone được repo từ internet trong local container | Chưa chạy `.NET` build/test; không được gọi `PASS` |

## 6. Trình tự thay đổi nhỏ nhất khi được phê duyệt code

1. **P0-C trước:** chứng minh block order + provenance mà không đụng behavior xử lý NĐ30 hiện có. Test đúng phạm vi parser/semantic evidence.
2. **P0-B tiếp:** Party regime + detector adapter (không thay administrative default), test Party positive + ND30 negative.
3. **P0-A cuối:** chỉ khi có canonical Party rule được thẩm định và manifest hợp lệ mới mở rộng tùy chọn nạp cùng `RuleCatalog`; không tạo release pack giả/empty `verified`.
4. **P1 formatting:** dựa trên `EffectiveFormattingResolver` hiện có, giải quyết mixed-runs và layout gates; `NOT_EVALUATED` khi thiếu capability.
5. **HD05 apply:** chỉ sau review source/time/authority, cục bộ bằng chứng patch; `signed/protected/secret/unsupported` fail-closed.

**Test hướng dẫn (chưa chạy):**
```bash
cd v22-runtime
dotnet restore DocumentEngine.slnx
dotnet build DocumentEngine.slnx --no-restore -c Release
dotnet test tests/DocumentEngine.Tests/DocumentEngine.Tests.csproj --no-build -c Release
dotnet test tests/SemanticDetector.Tests/SemanticDetector.Tests.csproj --no-build -c Release
dotnet test tests/LegalValidator.Tests/LegalValidator.Tests.csproj --no-build -c Release
```

Phạm vi xác minh mở rộng chỉ khi code thực tế ảnh hưởng contracts đang LOCKED. **Không thực hiện build/test giả lập hay ghi nhận PASS khi không có log máy chạy.**

## 7. Checklist nghiệm thu và trạng thái

| Gate | Điều kiện nghiệm thu | Trạng thái |
|---|---|---|
| Evidence audit | Source + exact symbol + logic dẫn tới kết luận | **PASS (static)** |
| Reuse before build | Phân biệt reuse native SDK / extension / code tối thiểu | **PASS (design)** |
| Parser preserves mixed order | Fixture có xen kẽ đoạn/bảng được chứng minh qua code + output | **NOT_TESTED / NO-GO** |
| Party detector independent routing | Party positive và ND30 negative tests chạy thực tế | **NOT_TESTED / NO-GO** |
| Party release loader | Release manifest verified + loader routing test | **BLOCKED** (chưa có verified Party rules) |
| Signed/unsupported safety | Không mutate và không báo PASS giả | **REQUIRES_RUNTIME_EVIDENCE** |
| 88 QA HD05 | Mapping sang fixture hiện có; test ảnh hưởng cần PASS | **SPECIFIED_ONLY** |
| Deploy / merge main | Có approval riêng và evidence | **NOT_AUTHORIZED** |

**Hành động trong vòng này:** chỉ bổ sung file Markdown vào **Draft PR #10**, không sửa code/CI/release pack, không merge/commit `main`, không bật HD05 auto-fix.

## 8. Đối chiếu thực thi P0-C từ PR #11 (17/10/2026)

**Báo cáo này được lập trước khi có mã thử nghiệm; phần trạng thái ở §7 phản ánh thời điểm nghiên cứu. Cập nhật mới hơn (có bằng chứng):**

- [PR #11](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/pull/11) triển khai nền tảng P0-C trên nhánh độc lập `feature/hd05-p0c-ordered-block-provenance`: `DocumentModel.BodyBlocks` bổ sung metadata nguồn, `DocxParser` duyệt trực tiếp `w:body` theo thứ tự, khối không xử lý được đánh dấu `unclassified`, giữ nguyên constructor và `Paragraphs/Tables`.
- [Workflow kiểm thử chuyên biệt thành công](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/actions/runs/39007188330): restore/build .NET 10 **PASS**; **3/3** test P0-C mới PASS; hồi quy **175/175** PASS (`DocumentEngine` 56, `SemanticDetector` 13, `LegalValidator` 106).
- [Workflow repository đầy đủ thành công](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/actions/runs/39007304541): chạy materializer, yêu cầu **zero source drift**, static safety audits, build .NET, targeted security/authorization/mutation tests, hồi quy **175/175**, host auth/safe errors/direct-alignment DOCX round-trip **PASS**. Bằng chứng ứng với commit `0754ae9ff286347bbea734d22b280968af2539f9`.
- Workflow đầy đủ đã được khôi phục cấu hình trigger gốc sau kiểm thử; nhánh PR #11 có một gate chuyên biệt hoạt động qua push/PR. Không lấy một lần kiểm thử của commit khác để tự nhận bản sửa code mới nếu production files có thay đổi; cần đối chiếu commit/diff.
- **Chưa hoàn thành toàn P0-C:** `AdministrativeSemanticDetector.Flatten()` vẫn có logic table-first phục vụ NĐ30, chưa được chuyển dùng `BodyBlocks` theo regime; chưa có nghiệm thu nguồn Party dạng `front/body/end/appendices`. Không tự động bật HD05 rules.
- **Quyết định cập nhật:** `P0C_FOUNDATION_IMPLEMENTED_AND_TESTED`; `P0C_END_TO_END_NOT_DONE`; `P0B_NOT_STARTED`; `P0A_BLOCKED_PENDING_VERIFIED_PARTY_RULES`; `PRODUCTION_AUTO_FIX_NO_GO`. PR #11 vẫn Draft và `main` không thay đổi.

Đối chiếu chi tiết: [ATOMIC-REUSE-AUDIT-V13.md](./ATOMIC-REUSE-AUDIT-V13.md), [GAP-AUDIT-V12.md](./GAP-AUDIT-V12.md).

## 9. Đối chiếu thực thi P0-B từ PR #12 (17/10/2026)

**Báo cáo V14 gốc được lập trước khi có mã P0-B.** Kết quả mới dưới đây thay thế trạng thái `P0B_NOT_STARTED` ở §8 nhưng **không** nâng quyền cho canonical Party rules hay HD05 auto-fix.

- [PR #12](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/pull/12) là **stacked PR** trên nhánh P0-C của [PR #11](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/pull/11), chỉ thay 3 file: bộ `PartySourceOrderSegmenter` (mã nguồn mới, không sửa `AdministrativeSemanticDetector`), 7 test xUnit và workflow riêng.
- Thành phần mới dùng trực tiếp `DocumentModel.BodyBlocks`, tạo 5 vùng **ứng viên** `Front/Body/End/Appendix/Unclassified` và gắn `BodyBlockReference`; yêu cầu tiêu đề Đảng chính xác, body/end anchors tường minh; thiếu mốc, source order không hợp lệ hoặc ownership trùng thì fail closed. Bảng và SDT chưa hỗ trợ được giữ `Unclassified`. Kết quả **luôn** yêu cầu rà soát; không có quyền tự động sửa.
- [GitHub Actions run #39026179901](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/actions/runs/39026179901) trên commit `0e9828a9ec43040f03b2f39016a2135a1f91717c`: .NET 10 restore/build **PASS**, **7/7** Party test PASS, full solution **182/182 PASS** (`DocumentEngine 56`, `SemanticDetector 20`, `LegalValidator 106`); không có failed/skipped. Pull-request run cùng commit [#39026223574](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/actions/runs/39026223574) cũng thành công.
- Còn **2 cảnh báo biên dịch nullable vốn có** ở `SafeDocxSnapshot.cs` dòng 74 và 79; cảnh báo xUnit2031 mới đã được sửa, không sửa subsystem bảo mật chỉ để xóa cảnh báo ngoài scope.
- **Chưa làm:** nhận diện tất cả biến thể thể thức Đảng (nhất là tiêu đề nằm trong bảng, công văn có nhiều dòng nơi nhận, tờ trình, đa chữ ký, phụ lục không có anchor điển hình), tích hợp đầu ra với Party regime routing/canonical validation, chạy QA 88 tình huống Hướng dẫn 05 trên fixture thực tế.
- **Quyết định hiện hành:** `P0B_CONSERVATIVE_CANDIDATE_SEGMENTER_IMPLEMENTED_AND_TESTED`; `P0B_FULL_TYPE_COVERAGE_NOT_DONE`; `P0A_BLOCKED_PENDING_VERIFIED_PARTY_RULES`; `PRODUCTION_AUTO_FIX_NO_GO`. Không merge PR #10/#11/#12 khi chưa review quyền/scope; `main` giữ nguyên.

## 10. Mở rộng tình huống P0-B — PR #13 (19/10/2026)

**PR mã nguồn:** [#13 — P0-B edge-cases](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/pull/13) đặt base trên PR #12; không cập nhật `main`, chưa merge và không bật các quy tắc Đảng tự sửa DOCX.

**Bổ sung tối thiểu, chỉ 2 file triển khai/test:** `PartySourceOrderSegmenter.cs`, `PartySourceOrderSegmenterTests.cs`.

- **Tiêu đề Đảng trong bảng:** nhận diện chính xác khi duy nhất một ô có chuỗi `ĐẢNG CỘNG SẢN VIỆT NAM` ở vùng đầu văn bản; cả bảng vẫn là `Unclassified` và yêu cầu rà soát. Nếu đồng thời phát hiện `CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM` trong vùng đầu thì trả `CONFLICTING_STATE_AND_PARTY_HEADINGS`.
- **Nhiều phụ lục:** thêm `PartyAppendixBoundary` có thứ tự, đường dẫn XML và nhãn nhận diện; trùng nhãn được cảnh báo. Không suy ra nội dung bảng/phụ lục đã được cấu trúc hóa hay ký hợp lệ.
- **Nhiều dòng ký:** thêm `PartySigningBoundary` (vị trí, prefix, đường dẫn nguồn), nhận diện mốc trong vùng End, không gán danh tính, thẩm quyền hoặc chữ ký số; khi nhiều tiền tố ký thì báo cần rà soát.
- **Fail-closed:** thiếu ranh giới phần thân/phần cuối, tiêu đề nằm trong bảng không rõ vùng, ký hiệu bất thường và khối không hỗ trợ không được tự tạo hoặc gán vào vùng nội dung xác thực.
- **Không thay đổi** `AdministrativeSemanticDetector`, bộ 435 rule ND30, `RuleCatalog`, các hành vi mutation/safety, hoặc DOCX runtime làm văn bản thật.

**Bằng chứng kiểm thử (19/10/2026):**

1. [P0-B focused + regression run 39210492274](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/actions/runs/39210492274): **13/13** test riêng P0-B và **188/188** test .NET toàn solution PASS, 0 failed/skipped.
2. [Full canonical verification run 39210613347](https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30/actions/runs/39210613347): **PASS** tại commit `e42ee36a7094c0f4c0ac3d5ce4bbaa2b3c6a8430`: materializer `zero source drift`, V22/V23 security/static audits, .NET 10 build, authorization/mutation tests, **188/188** full regressions, host boundary và direct-alignment DOCX round-trip PASS.
3. Trigger workflow dùng chung đã được trả về trạng thái gốc. Chỉ hai tệp P0-B mới xuất hiện trong diff PR #13 đối với base PR #12; sau full-gate chỉ thay đổi trigger workflow. Các kiểm thử được ghi ở đây là bằng chứng thực tế nhưng **không chứng minh** bao phủ đủ mọi mẫu thể thức Party.

**Giới hạn:** Những mốc ở đây là *candidate*, luôn `RequiresReview=true`. Chưa có canonical Party verified rules, chưa có Party release manifest hoặc thẩm định 88 tình huống QA theo quy định, chưa phân tích đầy đủ SDT/drawing/nested table, chưa tự động sửa văn bản theo Hướng dẫn 05. **P0-A runtime NO-GO** cho tới khi có quyền nguồn/authority, source locator, xác minh pháp lý và test cần thiết.

