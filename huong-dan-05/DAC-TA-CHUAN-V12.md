# ĐẶC TẢ CHUẨN HỢP NHẤT V12 — CẤU TRÚC VÀ THỂ THỨC VĂN BẢN CỦA ĐẢNG

**Đối chiếu:** Hướng dẫn 05-HD/VPTW ngày 27/5/2026 và Quy định 399-QĐ/TW ngày 9/1/2026  
**Phiên bản:** V12 — 17/10/2026  
**Loại tài liệu:** Kết quả nghiên cứu, đối chiếu nguồn, đặc tả kiểm tra và đề xuất triển khai DOCX. **Không thay thế văn bản có thẩm quyền.**  
**Trạng thái:** `REVIEW_READY / IMPLEMENTATION_NOT_APPROVED`; nguồn cốt lõi đã được đối chiếu, nhưng rule-runtime và kiểm thử DOCX chưa được nghiệm thu.  
**Phương pháp:** Hợp nhất kết luận có căn cứ từ V1–V11; không nối tiếp toàn bộ nhật ký cũ; không thiết kế engine mới, không sửa code/repository.

> **Thứ tự đọc áp dụng:** Nội dung chuẩn trong V12 ưu tiên hơn nhận định diễn giải đã bị thay thế ở V1–V11. Khi có khác biệt với quy định gốc hoặc văn bản sửa đổi mới, nguồn có thẩm quyền vẫn là căn cứ quyết định. V11 được giữ nguyên như hồ sơ lịch sử; xem liên kết tương ứng ở cuối báo cáo.

## 1. Kết luận chuẩn và phạm vi

1. **Không tồn tại con số bắt buộc “3/4/6 phần” cho mọi văn bản của Đảng.** Điều 15 Quy định 399 quy định **9 thành phần thể thức bắt buộc** đối với văn bản chính thức. Điều 16 có thành phần bổ sung theo trường hợp; Điều 17 dành riêng cho bản sao. Hướng dẫn 05 cụ thể hóa và định vị các thành phần này. Số cấp **Phần / Chương / Mục / Điều / Khoản / Điểm** trong **nội dung** không cố định (Hướng dẫn 05, II.I.6.1.6).
2. **Mô hình dữ liệu khuyến nghị:** `front`, `body`, `end` là 3 vùng lõi; `appendices[]`, `overlays[]`, `copy_metadata` là các lớp có điều kiện; `unclassified[]` chứa phần chưa xác định. Các tên trên **không phải tên vùng pháp lý** do Hướng dẫn 05 quy định.
3. **Phạm vi loại:** Quy định 399 Điều 5 liệt kê 25 thể loại; Điều 6 có 8 loại văn bản/giấy tờ hành chính; Điều 7 là văn bản chuyên ngành. Hướng dẫn 05 Phần I.1.1 loại trừ văn bản chuyên ngành, văn bản in thành sách và ấn phẩm khác; không áp chung một mẫu cho tất cả.
4. **Hiệu lực theo ngày:** Quy định 399 từ 09/01/2026; Hướng dẫn 05 từ 27/05/2026. Kiểm tra tài liệu lịch sử theo thời điểm ban hành và quan hệ sửa đổi/bãi bỏ, không dùng ngày sửa tệp DOCX hoặc ngày hiện tại thay thế.
5. **Công cụ định dạng không thay thế người ký, văn thư, kiểm tra thẩm quyền hay quy trình xác thực chữ ký số.** Những nội dung chưa thể xác minh phải ghi `NOT_EVALUATED`/`NEEDS_REVIEW`, không được báo `PASS`.

### 1.1. Phân biệt bốn lớp

| Lớp | Khái niệm | Tính bắt buộc | Sở hữu kỹ thuật |
|---|---|---|---|
| Nguồn quy định | 9 thành phần chính, thành phần bổ sung, thành phần sao | Theo loại/trường hợp | Rule source registry |
| Bố cục nội dung | Phần, chương, mục, điều, khoản, điểm | Không ấn định số lượng | `body` và cây nội dung |
| Phân vùng để xử lý | Phần đầu, thân, phần cuối | Đề xuất kỹ thuật | `front/body/end` |
| Cấu trúc mở rộng | Phụ lục, dấu chỉ dẫn, bản sao, thành phần chưa phân loại | Theo điều kiện | `appendices[]`, `overlays[]`, `copy_metadata`, `unclassified[]` |

## 2. Sổ nguồn và thứ tự thẩm quyền

| ID tài liệu | Văn bản/nguồn | Nội dung áp dụng | Trạng thái xác minh |
|---|---|---|---|
| `VN-PARTY-QD399-2026` | Quy định 399-QĐ/TW, 09/01/2026 | Điều 5–7: phân loại; Điều 14–17: thể thức; Điều 18: hiệu lực | **Đã đối chiếu** văn bản công bố, chưa có kiểm chứng trạng thái từng văn bản nguồn dẫn chiếu |
| `VN-PARTY-HD05-2026` | Hướng dẫn 05-HD/VPTW, 27/05/2026 | Phần I–III; Phụ lục 1–3; cách trình bày cụ thể | **Đã đối chiếu trực tiếp PDF 36 trang** và xem sơ đồ/bảng phụ lục |
| `VN-PARTY-HD03-2021` | Hướng dẫn 03-HD/VPTW, 04/06/2021 | Chữ ký số, được HD05 dẫn chiếu | **Đã định danh nguồn**, chưa hoàn tất kiểm tra hiệu lực cấp điều/khoản; không bật tự sửa |
| `VN-PARTY-QD379-2025`, `VN-PARTY-HD09-2026` | Giao dịch, xử lý văn bản điện tử | Nghiệp vụ/vòng đời điện tử | Nguồn liên quan, **không tự thay thế** quy tắc bố cục HD05 |
| `VN-PARTY-QD383-2025`, `VN-PARTY-HD06-2026` | Văn thư và lưu trữ | Bảo quản và hồ sơ | Nguồn liên quan, chưa đưa thành rule trình bày |
| Luật và nghị định về bí mật nhà nước đang áp dụng | Nguồn quản lý an toàn | Xác định quyền xử lý tài liệu mật | **Yêu cầu xác minh ở môi trường có thẩm quyền**, không truy cập hay tải dữ liệu mật khi chưa được phép |

**Tính kế thừa:** Quy định 399 Điều 18 thay Quy định 66-QĐ/TW (2017), Quy định 223-QĐ/TW (2020) và nêu bãi bỏ công văn liên quan; Hướng dẫn 05 Phần III.1 thay Hướng dẫn 36-HD/VPTW (2018) và nêu bãi bỏ các công văn. Hai nguồn ghi **chuỗi số/ký hiệu Công văn 5053 khác nhau** (đặc biệt đuôi `/nb`); lưu cả hai định danh nguyên văn và đánh dấu `ALIAS_UNRESOLVED`, không tự gộp, không suy rằng văn bản đã bãi bỏ lại có hiệu lực trong giai đoạn giữa hai mốc.

**Nguồn gốc công khai:**

- Hướng dẫn 05, PDF 36 trang: https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf
- Cổng tư liệu Hướng dẫn 05: https://tulieuvankien.dangcongsan.vn/van-ban-cua-dang/huong-dan-so-05-hd-vptw-ngay-27-5-2026-cua-van-phong-trung-uong-dang-ve-the-thuc-va-ky-thuat-trinh-bay-van-ban-cua-dang.html
- Quy định 399, toàn văn công bố: https://xaydungchinhsach.chinhphu.vn/quy-dinh-so-399-qd-tw-cua-ban-bi-thu-ve-the-loai-tham-quyen-ban-hanh-va-the-thuc-van-ban-cua-dang-119260119124508363.htm

Trong tất cả bảng phía dưới: `II.I.5.2.1` nghĩa là Phần II, Mục I, khoản/tiểu mục 5.2.1 của Hướng dẫn 05; `PL1/PL2/PL3` là ba phụ lục. **Số trang PDF là 1-based** (PDF trang 1 là trang xuất hiện đầu tiên), không phải số trang nội bộ in ở chân giấy.

## 3. Mô hình trường thể thức — 9 thành phần và các ô Phụ lục 1

| Thành phần quy định | Ô PL1 | Vai trò (semantic role) | Vùng sở hữu mặc định | Ngoại lệ/ràng buộc |
|---|---|---|---|---|
| 1. Tiêu đề “Đảng Cộng sản Việt Nam” | 1 | `party_heading` | `front` | Đường nét liền bên dưới dài bằng tiêu đề |
| 2. Tên cơ quan ban hành | 2 | `issuing_authorities[]` | `front` | Có thể nhiều dòng, liên cơ quan |
| 3. Số và ký hiệu | 3 | `document_number` | `front` | Không được tự cấp/sửa số đăng ký |
| 4. Địa danh và ngày ban hành | 4 | `issue_place_and_date` | `front` | Phải căn cứ địa danh/thời điểm thực tế |
| 5. Tên loại và trích yếu | 5a, 5b | `document_type`, `subject` | `front` | Công văn chỉ có trích yếu tại 5b; `Kính gửi` ở 5a mang role nơi nhận |
| 6. Nội dung | 6 | `body` và `legal_bases[]` | `body` | Phần căn cứ, các bảng/ảnh/chương/điều nằm trong body nếu thuộc nội dung |
| 7. Quyền hạn, chức vụ, họ tên và chữ ký | 7a, 7b, 7c | `signatures[]` | `end` | Có thể nhiều người ký, xác nhận, liên cơ quan |
| 8. Dấu cơ quan | 8 | `seals[]` | `end`/anchor | Không tạo lại con dấu, không di chuyển khi chưa xác nhận |
| 9. Nơi nhận | 9a, 9b; riêng công văn dùng 5a | `recipients_end[]`, `recipients_start[]` | `end` và `front` | Công văn “Kính gửi” ô 5a; tờ trình “Kính trình” ô 9b; end có “Như trên” theo quy định |

**Thành phần bổ sung PL1:** ô 10 = dấu khẩn; 11a = phạm vi lưu hành; 11b = dự thảo; 12 = người soạn thảo/số lượng phát hành; 13 = liên hệ; 14 và 15 = chữ ký số của cơ quan và cá nhân trên **bản sao sang điện tử**. Mỗi trường neo đúng vị trí, không gom mọi overlay thành một vùng cuối tài liệu. Phụ lục là thành phần bổ sung nhưng cần `appendices[]` riêng. Nguồn: Hướng dẫn 05 II.II và PL1, PDF trang 30.

**Không nhầm hệ mã:** Các số 1–15 **ô bố trí PL1** KHÁC các dòng 1–15 trong **bảng cỡ chữ PL2**. PL3 lại có các ký hiệu sơ đồ `1`, `2a`, `2b`, `2c`, `3` chỉ thành phần bản sao giấy, không phải các ô PL1.

## 4. Phân loại và định tuyến trường hợp áp dụng

### 4.1. Danh mục phân loại theo Quy định 399

**25 thể loại (Điều 5):** Cương lĩnh chính trị; Điều lệ Đảng; Chiến lược; Nghị quyết; Quyết định; Chỉ thị; Kết luận; Quy chế; Quy định; Thông tri; Hướng dẫn; Thông báo; Thông cáo; Tuyên bố; Lời kêu gọi; Báo cáo; Kế hoạch; Quy hoạch; Chương trình; Đề án; Phương án; Dự án; Tờ trình; Công văn; Biên bản.

**8 loại giấy tờ hành chính (Điều 6):** Giấy giới thiệu; Giấy chứng nhận; Giấy đi đường; Giấy nghỉ phép; Giấy mời; Phiếu chuyển; Phiếu gửi; Thư công.

**Điều 7:** Văn bản chuyên ngành theo cơ quan có thẩm quyền; Hướng dẫn 05 Phần I.1.1.c **loại trừ** khỏi phạm vi điều chỉnh của hướng dẫn này. Loại tài liệu này cần bộ quy tắc riêng hoặc `NOT_APPLICABLE`/`NEEDS_CLASSIFICATION`, không tự áp PL2 cho mọi trường.

### 4.2. Bộ quyết định tối thiểu (không tự suy thẩm quyền)

| Ngữ cảnh | Phân loại/đầu ra | Tự sửa? |
|---|---|---|
| Cơ quan Đảng, văn bản có tên loại hợp lệ, ngày sau 27/05/2026, DOCX chưa ký | `party + standard_document`, HD05 áp dụng theo trường | Chỉ cân nhắc sửa định dạng an toàn |
| Công văn | `party + official_letter`, ô 5b trích yếu, ô 5a “Kính gửi” | Không tự sinh tên loại hay “-----” |
| Tờ trình | `party + proposal`, ô 9b “Kính trình” | Không chuyển người nhận thành nội dung |
| Giấy tờ Điều 6 | `party + administrative_paper` | A4/A5/mẫu in sẵn; theo mẫu có chứng cứ |
| Văn bản chuyên ngành, sách, ấn phẩm | `specialized/out_of_scope` | Không auto-fix theo HD05 |
| Văn bản của tổ chức/doanh nghiệp nằm ngoài đối tượng HD05 | `outside_subject_scope` | Không áp tự động |
| Ban hành 09/01–26/05/2026 | `QD399_in_force, HD05_not_yet_in_force` | Dùng chuẩn từng thời điểm; không áp hồi tố |
| Không rõ `regime`, người ký, thời điểm hoặc trạng thái chữ ký | `NEEDS_CLASSIFICATION / NEEDS_REVIEW` | Không sửa cấu trúc |
| Văn bản có chữ ký số hoặc đã phát hành | `AUDIT_ONLY` | Không sửa bản gốc |
| Tài liệu mật hoặc phân loại bảo mật chưa xác nhận | `SECURITY_REVIEW` | Không xử lý ngoài phạm vi/phương tiện được phép |

### 4.3. Đặc thù không suy chỉ từ từ khóa

- **Công văn:** Không có dòng tên loại ở chính giữa như báo cáo/quyết định; trích yếu ở dưới số, ký hiệu (II.I.5.2.3). “Kính gửi” dùng ô 5a nhưng **thuộc vai trò nơi nhận** (II.I.9.2.3).
- **Tờ trình:** Có tên loại/trích yếu, “Kính trình” dưới đó tại ô 9b và vai trò nơi nhận (II.I.9.2.4).
- **Báo cáo chính trị trình đại hội:** có thể dùng chủ đề đại hội thay tên loại/trích yếu thông thường (II.I.5.1.2).
- **Biên bản:** có người ghi/chủ trì và có thể có xác nhận chữ ký (II.I.7.1.1.e), không giả định chỉ một người ký.
- **Liên cơ quan:** cơ quan chủ trì trước; ký hiệu, địa danh và người ký có quy tắc riêng, cần chứng cứ về cơ quan chủ trì.
- **Số của đại hội:** một dãy chung từ 01 cho các loại văn bản của đại hội và các cơ quan của đại hội từ khai mạc đến bế mạc (II.I.3.1.1.a). **Số cấp ủy thông thường:** mỗi tên loại có dãy riêng theo nhiệm kỳ với ngoại lệ ngày bắt đầu nhiệm kỳ (II.I.3.1.1.b). Không thể xác minh đầy đủ bằng một DOCX đơn lẻ.

## 5. Bản đồ định dạng chuẩn từ Phụ lục 2

**Chỉ những ô dưới đây đã được đối chiếu trực tiếp với bảng mẫu PL2**; định dạng của các thành phần khác phải tra bảng gốc hoặc quy định chuyên biệt, không suy ra từ tên trường.

| Thành phần | Cỡ chữ (pt) | Kiểu/chữ | Locator |
|---|---:|---|---|
| Tiêu đề ĐẢNG CỘNG SẢN VIỆT NAM | 15 | Hoa, đứng, đậm | PL2, TT1, PDF tr.31 |
| Tên cấp trên trực tiếp (nếu có) | 14 | Hoa, đứng | PL2, TT2, tr.31 |
| Tên cơ quan ban hành | 14 | Hoa, đứng, đậm | PL2, TT2, tr.31 |
| Số, ký hiệu | 14 | Thường, đứng | PL2, TT3, tr.31 |
| Địa danh, ngày tháng | 14 | Thường, nghiêng | PL2, TT4, tr.31 |
| Tên loại văn bản | 15–16 | Hoa, đứng, đậm | PL2, TT5, tr.31 |
| Trích yếu văn bản có tên loại | 14–15 | Thường, đứng, đậm | PL2, TT5, tr.31 |
| **Trích yếu công văn** | **12** | Thường, **nghiêng** | PL2, TT5, tr.32 |
| Nội dung văn bản thường | 14–15 | Thường, đứng | PL2, TT6, tr.32 |
| Phần, Chương và số | 14–15 | Thường, đứng, đậm | PL2, TT6, tr.32 |
| Tên Phần, Chương | 14–15 | Hoa, đứng, đậm | PL2, TT6, tr.32 |
| Mục và tên Mục | 14–15 | Số Mục đứng đậm; tên Mục hoa, đậm | PL2, TT6, tr.32 |
| Điều và tên Điều | 14–15 | Thường, đứng, đậm | PL2, TT6, tr.32 |
| Khoản / Điểm | 14–15 | Thường, đứng | PL2, TT6, tr.32 |
| Quyền hạn ký | 14 | Hoa, đứng, đậm | PL2, TT7, tr.32–33 |
| Chức vụ người ký | 14 | Hoa, đứng | PL2, TT7, tr.33 |
| Họ tên người ký | 14 | Thường, đứng, đậm | PL2, TT7, tr.33 |
| “Kính gửi”/“Kính trình” | 14 | Thường, nghiêng | PL2, TT9, tr.33 |
| Danh sách sau “Kính gửi”/“Kính trình” | 14–15 | Thường, đứng | PL2, TT9, tr.33 |
| “Nơi nhận” | 14 | Thường, đứng, có gạch chân | PL2, TT9, tr.33; II.I.9.2.1 |
| Danh sách sau “Nơi nhận” | 12 | Thường, đứng | PL2, TT9, tr.33 |
| Dấu chỉ mức độ khẩn | 14 | Hoa, đứng, đậm | PL2, TT11, tr.33 |
| Chỉ dẫn phạm vi lưu hành | 12 | Hoa, đứng, đậm | PL2, TT12, tr.34 |
| Chỉ dẫn dự thảo | 14 | Thường, nghiêng | PL2, TT12, tr.34 |
| Ký hiệu người soạn thảo, số lượng phát hành | 11 | Thường, đứng | PL2, TT13, tr.34 |
| Thông tin liên hệ | 10 | Thường, đứng | PL2, TT14, tr.34 |
| Số trang | 14 | Thường, đứng | PL2, TT15, tr.34 |

**Khổ, lề và dòng theo II.IV và II.I.6.2.2:** A4 (210×297 mm) cho văn bản thuộc quy tắc chung; giấy tờ hành chính được A4/A5 (148×210 mm)/mẫu in sẵn; lề trang dọc trên/dưới **20/20 mm**, trái/phải **30/15 mm**; mặt sau khi in hai mặt trái/phải **15/30 mm**; bảng/biểu có thể bố trí ngang. Thân văn bản dàn đều hai lề, thụt đầu dòng khoảng **10 mm**, cách đoạn **tối thiểu 6 pt**, giãn dòng **Exactly 18–22 pt**. Số trang ở giữa phía trên, cách mép trên **10 mm**, không hiển thị trang thứ nhất; mỗi phụ lục đánh số trang **riêng**.

> **Thận trọng về “đúng”:** Các khoảng giá trị là khoảng hợp lệ; không tự biến thành một trị chuẩn tùy ý. “Exactly 18–22 pt” khác “Multiple 1.2”. Tài liệu in hai mặt phải có bằng chứng ý định in, không tự đảo lề dựa trên trang chẵn/lẻ khi chế độ chưa rõ. Bảng ngang phải xét section/layout thực tế.

## 6. Phân biệt dấu, đường kẻ, ký tự và dấu câu

| Thành phần | Bản chất cần nhận diện | Vị trí/nguồn | Không được nhầm với |
|---|---|---|---|
| Đường dưới tiêu đề Đảng | Đường kẻ **nét liền**, dài bằng tiêu đề | Phía dưới tiêu đề, II.I.1.2, PL1 | Năm dấu `-----` |
| Dấu `*` dưới cơ quan | **Ký tự dấu sao** theo mẫu | Sau tên cơ quan, II.I.2.2.1 | Dấu sao nằm trong bảng/nội dung |
| `-----` sau trích yếu loại văn bản thường | **5 dấu gạch nối** | II.I.5.2.1, PL1 | Gạch đầu mỗi căn cứ hoặc đường viền |
| `-` trước căn cứ | Một dấu mở đầu từng căn cứ, căn cứ thuộc body | II.I.6.2.3 | Đường phân vùng/5 dấu gạch |
| `-` trong số/ký hiệu và giữa liên cơ quan | Thành tố text của metadata | II.I.3.2.2 | Dấu phân cách nội dung |
| Đường dưới “Nơi nhận” | Đường nét liền theo chiều dài cụm từ | II.I.9.2.1 | Gạch chân ký tự theo PL2; cần kiểm riêng cả hai |
| Đường phía trên liên hệ | Đường kẻ nét liền **toàn chiều ngang vùng trình bày** | II.II.6 | Đường phía trên phần sao |
| Đường ngăn phần bản sao giấy | Đường nét liền **toàn chiều ngang vùng trình bày** | II.III.2.2 | Đường liên hệ; phải xét dấu bản sao/chữ ký sao kèm theo |

**Căn cứ (II.I.6.2.3):** mỗi căn cứ là dòng/khổ riêng, bắt đầu bằng `-`, kết thúc `;`, riêng căn cứ cuối kết thúc `.`. **Tất cả thuộc body.** Cấu trúc dấu câu trong danh sách người nhận cần kiểm ở II.I.9.2.5; chỉ cảnh báo nội dung khi thiếu ngữ cảnh, không tự xóa/viết lại người nhận.

**Ngày và tháng:** II.I.4.2.2 ghi nguyên văn điều kiện **“ngày dưới 10 và tháng dưới 3”** thêm số 0. Không tự biến thành “tháng dưới 10” theo Nghị định 30 hay suy lý giải mới. Khi dữ liệu hồ sơ/ngữ cảnh chưa đầy đủ, chỉ đánh dấu cần kiểm duyệt chứ không đổi ngày ban hành.

## 7. Phụ lục văn bản và bản sao

### 7.1. Phụ lục

- Phụ lục phải có **chỉ dẫn kèm theo** trong văn bản chính; trình bày riêng, có tiêu đề, số/ký hiệu/ngày văn bản và tên cơ quan ban hành ở phần dẫn chiếu dưới tiêu đề.
- Chỉ từ **hai phụ lục trở lên** mới phải ghi số thứ tự Ả-rập theo II.II.1; một phụ lục không cưỡng chế “Phụ lục 1” nếu mẫu gốc không có.
- `appendices[]` lưu đủ bảng, ảnh, đoạn, số trang và section từng phụ lục; **mỗi phụ lục đánh số trang riêng**. Không dồn phụ lục vào body hay sao chép lại nội dung phụ lục trong body.

### 7.2. Bản sao

**7 thành phần bản sao** từ Điều 17 Quy định 399: cơ quan sao; số và ký hiệu; hình thức sao; địa danh-ngày sao; quyền hạn/chức vụ/họ tên/chữ ký người ký sao; dấu cơ quan sao; nơi nhận bản sao. Đây là thẩm quyền của cơ quan sao, không phải fields thuộc bản chính.

**Sao ra giấy (HD05 II.III.2, PL3 PDF tr.35–36):** dấu “Bản sao văn bản” bên trái phía dưới nơi nhận văn bản cần sao; đường kẻ toàn vùng ngăn với bản được sao; phần người ký sao bên phải; dấu cơ quan sao theo quy định. Mẫu dấu hình chữ nhật **80×50 mm**, mực **đỏ**, dòng “BẢN SAO VĂN BẢN” hoa, Times New Roman **13 pt**, đứng, đậm; ký hiệu bản sao chung là `BS`, số bắt đầu 01 dùng chung các hình thức sao trong một nhiệm kỳ cấp ủy theo chú giải PL3. **Không được tự tạo dấu thật, chữ ký hoặc bản sao có giá trị xác thực bằng thao tác định dạng.**

**Sao sang điện tử (HD05 II.III.3):** chữ ký số cơ quan sao ở **ô 14 PL1**, chữ ký số cá nhân người sao ở **ô 15 PL1**, cả hai theo quy định chữ ký số áp dụng. Ảnh con dấu/chữ ký không chứng minh giá trị chữ ký số. Không sửa gói tài liệu đã ký để giữ chữ ký hợp lệ.

## 8. Hợp đồng dữ liệu trung gian DOCX — đặc tả, chưa phải implementation

```yaml
DocumentState:
  regime: {value: party|administrative|specialized|unknown, confidence: ...}
  document_type: {value: ..., evidence: ...}
  issue_date: {value: ..., evidence: ..., temporal_status: ...}
  lifecycle: {stage: draft|issued|signed|copy|unknown}
  security: {classification: ..., permission_evidence: ...}
  source_manifest: {parts: [...], relationships: [...], sections: [...], hash: ...}
  front: {nodes: [...], issuing_authorities: [...], document_number: ..., recipients_start: [...]}
  body: {nodes: [...], legal_bases: [...], outline: [...], tables: [...], images: [...]}
  end: {nodes: [...], signatures: [...], recipients_end: [...], seals: [...]}
  appendices: [{id: ..., nodes: [...], tables: [...], images: [...], numbering_scope: ...}]
  overlays: [{role: ..., anchor: ..., nodes: [...]}]
  copy_metadata: {kind: paper|electronic|null, fields: [...], evidence: ...}
  unclassified: [{source_node_id: ..., reason: ..., candidates: [...]}]
```

Các khóa trên chỉ là **mô hình khái niệm**. Khi triển khai phải ánh xạ vào `DOCUMENT-MODEL.md`, `RULE-MODEL.md`, `APPLICABILITY.md` hiện có; không tạo authority song song.

### 8.1. Bất biến dữ liệu

1. **Một nguồn, một chủ sở hữu:** mỗi đoạn/bảng/ảnh/con trỏ nguồn chỉ có một owner; một tham chiếu phụ lục trong body không có nghĩa nhân đôi nội dung phụ lục.
2. **Không mất/trùng/đảo thứ tự:** mọi source block phải được ánh xạ một lần hoặc chuyển vào `unclassified[]`; record giữ OOXML part, vị trí, ID và quan hệ anchor.
3. **Vai trò ≠ vị trí:** `Kính gửi` và `Kính trình` là nơi nhận dù hiển thị phần trên tài liệu; chữ ký xác nhận trong biên bản có thể xuất hiện nhiều khối.
4. **Lựa chọn tin cậy:** không biết giới hạn vùng thì không tự chuyển dữ liệu sang body; mức suy luận thấp cho `NEEDS_REVIEW`.
5. **Không giả PASS:** bộ đọc thiếu khả năng kiểm tra stamp/drawing/AlternateContent/field/chữ ký số thì trả `NOT_EVALUATED` hoặc chặn sửa phù hợp.
6. **Không lưu nội dung tài liệu khi không được phép:** không upload, cache nội dung, log nguyên văn hoặc gửi bên thứ ba đối với tài liệu bị hạn chế quyền; phân tích tại môi trường được phép.

### 8.2. Các thành phần OOXML cần lập manifest bảo toàn

- `word/document.xml`, `styles.xml`, `numbering.xml`, các `sectPr`, footnotes/endnotes, comments và revisions.
- Tables (kể cả lồng nhau), images inline/anchor, shapes/textboxes/drawings, VML, OLE/charts, hyperlinks, relationship IDs và media parts.
- `mc:AlternateContent`, `w:altChunk`, custom XML, headers/footers khác nhau theo first/odd/even, content controls, fields số trang.
- Chữ ký số OOXML/OPC, encryption/protection, macros, embedded files: không xử lý thay đổi khi capability chưa được chứng minh.

*Chính sách an toàn:* Nếu engine không hiểu một đối tượng có thể bị ảnh hưởng bởi phép sửa, **không** xuất kết quả và khẳng định nguyên vẹn; ghi blocker/scope rõ ràng. Thử sửa giá trị lề/khổ giấy là thay đổi cấp section với blast radius lớn, cần render và round-trip kiểm chứng.

## 9. Ma trận quy tắc ưu tiên có dẫn nguồn — ứng viên, chưa phê duyệt runtime

Những ID `HD05-V12-*` dưới đây là **mã tra cứu tài liệu**, **không được coi là canonical rule ID** trong repository. Nếu một dòng có nhiều thuộc tính mô tả, khi import phải tách nguyên tử theo `RULE-MODEL.md` và đối chiếu với rule hiện có trước.

| Mã tham chiếu | Một yêu cầu/đối tượng kiểm tra | Căn cứ chuẩn | Phương pháp và giới hạn |
|---|---|---|---|
| R01 | Tiêu đề Đảng = 15 pt | PL2 TT1 | OOXML effective font size |
| R02 | Đường dưới tiêu đề = nét liền dài bằng tiêu đề | II.I.1.2, PL1 | Cần layout/render, không kiểm bằng HTML text |
| R03 | Dấu `*` đúng ngữ cảnh dưới cơ quan | II.I.2.2.1 | Ký tự + bối cảnh, không nhầm body |
| R04 | Số văn bản nhỏ hơn 10 viết có 0 | II.I.3.2.2 | Kiểm chuỗi; số cấp phát cần sổ văn thư |
| R05 | Số/ký hiệu có dấu `-` và `/` đúng chức năng | II.I.3.2.2 | Parser metadata; không sửa số đã cấp |
| R06 | Ngày dưới 10 viết có 0 | II.I.4.2.2 | Cảnh báo ngày; không sửa ngày nghiệp vụ |
| R07 | Tháng dưới 3 viết có 0 | II.I.4.2.2 | Giữ nguyên câu nguồn, không mở rộng |
| R08 | Tên loại = 15–16 pt (nếu loại thường) | PL2 TT5 | Trường `document_type` |
| R09 | Trích yếu có tên loại = 14–15 pt, đứng đậm | PL2 TT5 | Trường `subject` không phải công văn |
| R10 | Trích yếu công văn = 12 pt, nghiêng | PL2 TT5 | Chỉ áp ô 5b |
| R11 | `-----` có 05 dấu khi văn bản có tên loại | II.I.5.2.1 | Không mặc định thêm cho công văn |
| R12 | Nội dung = 14–15 pt | PL2 TT6 | `body` only |
| R13 | Nội dung căn đều hai lề | II.I.6.2.2 | Kiểm alignment hiệu dụng |
| R14 | Thụt đầu dòng khoảng 10 mm | II.I.6.2.2 | Kiểm per-paragraph/style |
| R15 | Khoảng cách đoạn tối thiểu 6 pt | II.I.6.2.2 | Kiểm spacing hiệu dụng |
| R16 | Giãn dòng Exactly 18–22 pt | II.I.6.2.2 | Kiểm mode và giá trị riêng |
| R17 | Căn cứ mở đầu `-`, `;` trừ căn cứ cuối `.` | II.I.6.2.3 | Semantic content review |
| R18 | Công văn “Kính gửi” ô 5a và tờ trình “Kính trình” ô 9b | II.I.9.2.3–4 | Kiểm type + location + role |
| R19 | “Nơi nhận” 14 pt; tên nơi nhận cuối 12 pt | PL2 TT9 | Tách thành 2 rule khi import |
| R20 | Phụ lục có tiêu đề, chỉ dẫn; >=2 đánh số Ả-rập | II.II.1 | Kiểm scope mỗi phụ lục |
| R21 | A4 bình thường, ngoại lệ giấy tờ A4/A5/mẫu in | II.IV.3 | Conditional page rule |
| R22 | Lề trang dọc 20/20/30/15 mm | II.IV.4.1 | Tách mỗi lề thành 1 rule; section-aware |
| R23 | Mặt sau in hai mặt trái/phải 15/30 mm | II.IV.4.2 | Cần duplex intent; không suy từ XML đơn |
| R24 | Số trang giữa phía trên, 10 mm; trang đầu ẩn | II.IV.5.1 | Layout/field/render; tách thuộc tính |
| R25 | Số trang của mỗi phụ lục độc lập | II.IV.5.2 | Kiểm section/field, cần xem tài liệu thực |
| R26 | Dấu bản sao giấy = 80×50 mm | PL3 §1.1 | Chỉ chứng minh trên vật mang tin/hình với bằng chứng đo |
| R27 | Mực dấu bản sao đỏ, tên dấu 13 pt hoa đậm | PL3 §1.1 | Không kết luận mực thật chỉ từ pixel |
| R28 | Ký hiệu bản sao `BS`, số từ 01 dùng chung trong nhiệm kỳ | PL3 §1.2 | Không tự cấp số/ấn định sổ |
| R29 | Hai chữ ký số trên bản sao sang điện tử | II.III.3, PL1 ô 14–15 | PKI verification, không suy ảnh thành chữ ký hợp lệ |

**Không sao chép rule:** Bảng có **29 nhóm tham chiếu R01–R29**; các nhóm phức hợp (chẳng hạn R19, R22, R23, R24) phải được phân rã thành các thuộc tính nguyên tử rồi **exact-dedup** với canonical rule hiện hữu. Không tạo hai authority cho cùng một constraint.

## 10. Mức bằng chứng, trạng thái kiểm tra và quyền tự sửa

| Mức | Có thể kết luận | Ví dụ |
|---|---|---|
| `SOURCE_CONFIRMED` | Câu quy định đã được đối chiếu PDF | Cỡ chữ trích yếu công văn 12 pt |
| `FIELD_CONFIRMED` | Khối DOCX có role và vị trí được chứng minh | Ô 5b từ DOCX nguồn |
| `VALIDATION_EVALUATED` | Engine đã đánh giá rule với evidence | w:rPr/style effective hoặc render |
| `FIX_VERIFIED` | Sau sửa đã mở lại và đối chiếu đủ blast radius | Chỉ trường chọn bị sửa, các vùng khác giữ nguyên |

**Kết quả validator** (đã có trong `VALIDATION-RESULTS.md`): `PASS`, `FAIL`, `NOT_APPLICABLE`, `NOT_EVALUATED`, `NEEDS_REVIEW`. Không đưa `NOT_EVALUATED` về `PASS`; coverage phải ghi số applicable/evaluated/untested.

| Hoạt động | DOCX dự thảo/chưa ký | Văn bản đã ký, bản sao đã xác thực | Nội dung chưa rõ nguồn/quyền |
|---|---|---|---|
| Đọc, tách cấu trúc | Cho phép có điều kiện quyền & an toàn | Chỉ audit ở phạm vi được phép | Cần phân loại |
| Kiểm format deterministic | Có nếu có capability/evidence | Read-only | `NOT_EVALUATED` nếu thiếu evidence |
| Auto-fix kiểu chữ trường xác định | Có điều kiện rule verified + user approval + round-trip | **NO-GO** | **NO-GO** |
| Auto-fix số ban hành, ngày ký, tên cơ quan, người nhận, chữ ký | **NO-GO** trong nhiệm vụ format | **NO-GO** | **NO-GO** |
| Bảo toàn bảng, ảnh, links, protections | **Bắt buộc** | Bảo toàn nguyên gói | Không xuất bản sửa nếu chưa chứng minh |

**Go/No-Go trước khi nói “đã chuẩn hóa”:** (i) chọn đúng nguồn/chế độ/hiệu lực; (ii) role/field và original owner không mâu thuẫn; (iii) rule được đối chiếu, capability có thật; (iv) test vùng bị sửa PASS; (v) round-trip chứng minh bảo toàn; (vi) người dùng đã phê duyệt hành động; (vii) có change log/diff/rollback, không có chữ ký số bị làm hỏng.

## 11. Bằng chứng tối thiểu của một phép sửa DOCX

```yaml
validation_result:
  rule_id: existing_canonical_rule_id
  source_id: VN-PARTY-HD05-2026
  source_locator: "II.I.6.2.2"
  applicability: {regime: party, type: ..., issue_date: ..., stage: draft}
  target: {source_node_id: ..., semantic_role: body, ooxml_part: "word/document.xml"}
  expected: {property: ..., value: ...}
  actual: {property: ..., effective_value: ...}
  status: PASS|FAIL|NOT_APPLICABLE|NOT_EVALUATED|NEEDS_REVIEW
  evidence: {xml: ..., style: ..., render: ..., confidence: ...}
  suggested_action: audit_only|review|safe_patch|blocked
change_record:
  approved_by_user: true
  before_manifest: ...
  after_manifest: ...
  unchanged_content_verified: ...
  unchanged_parts_verified: ...
  reopened_docx_result: ...
  affected_tests: ...
```

**Lưu ý:** YAML này là **mô tả giao diện dữ liệu**, chưa sinh trong code. Không được ghi `approved_by_user: true` khi không có phê duyệt thực tế. Hash của package DOCX sẽ thay đổi khi sửa có chủ đích; điều cần chứng minh là tính toàn vẹn nội dung/đối tượng không thuộc phạm vi sửa, không phải luôn giữ hash package cũ.

## 12. Đối chiếu kiến trúc GitHub và chính sách REUSE FIRST

**Repository tham chiếu:** https://github.com/quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30  
**HEAD xác minh trong vòng kiểm toán:** `078dae21f9e23975676179fa189f5c01b605f49a` (02/10/2026). Không suy rằng commit này có đầy đủ engine HD05.

| Đã có trong repo | Cách tái sử dụng, không nhân đôi |
|---|---|
| `DOCUMENT-MODEL.md` | Semantic components, source evidence, section-aware layout; mở rộng vai trò tối thiểu cho văn bản Đảng |
| `APPLICABILITY.md` | Phân loại regime, chủ thể, ngày, trạng thái, bảo mật; không router riêng mới |
| `RULE-MODEL.md` | Atomic constraints, locator, applicability, verified rules; không viết rule engine mới |
| `VALIDATION-RESULTS.md` | Trạng thái đánh giá và coverage; không xây schema báo cáo mới |
| `sources/registry.yaml` | Đã đăng ký QĐ399/HD05; kiểm source manifests/locator trước khi bổ sung |
| `rule-packs/party-document-format-2026.yaml` | Đã include QĐ399 và HD05; cấu hình rule pack, chưa chứng minh rule thực thi đủ |
| `huong-dan-05/README.md` | Hiện chỉ là placeholder; có thể dùng **tài liệu V12** làm đầu vào biên soạn đặc tả trong repo nếu được yêu cầu |

**Trình tự tích hợp khi có giao nhiệm vụ coding:** audit canonical IDs đã có → mapping rule ứng viên thành rule verified bị thiếu → dùng document engine đã có → validate trên fixture ảnh hưởng → patch nhỏ tại field → mở lại DOCX → log evidence. Không commit, push, deploy, migrate hoặc chỉnh nguồn hiện hữu trong phạm vi báo cáo này.

## 13. Lỗi dễ gặp và phân xử theo bằng chứng

| Rủi ro | Tác hại | Phương án đúng |
|---|---|---|
| Nhầm `body` với toàn bộ `document.xml` | Đưa chữ ký/nơi nhận vào thân | Phân vùng semantic, giữ provenance |
| Dùng HTML/mammoth text đơn lẻ để kết luận đường kẻ/font | False PASS/FAIL | Kiểm OOXML effective properties và ảnh render |
| Ép công văn theo template quyết định | Thêm nhan đề/`-----` sai | Định tuyến `official_letter` trước |
| Gộp `Kính trình` vào body | Sai trích xuất nội dung | Vị trí ô 9b nhưng role nơi nhận |
| Nhầm bảng/phụ lục có từ “Phần I” thành heading cấp cao | Mất cây nội dung | Kết hợp nguồn, vị trí, bố cục và context |
| Tự tính số văn bản từ chuỗi | Sai đăng ký văn thư | Chỉ cảnh báo, xác minh sổ cấp số |
| Tự sửa tháng 03–09 thành 0x | Có thể không đúng HD05 | Giữ đúng điều kiện tháng dưới 3 |
| Dùng rule lề chung cho mọi section | Hỏng phụ lục/bảng ngang | Section-aware và round-trip |
| Gán stamp bản sao theo dấu `*` | Sai loại thể thức | PL3 vs PL1, evidence khác nhau |
| Kết luận chữ ký số hợp lệ từ ảnh | Sai xác thực điện tử | Dùng kết quả xác thực PKI có thẩm quyền |
| Thay styles toàn cục để sửa 1 ô | Lan lỗi các ô khác | Patch target có blast radius nhỏ nhất |
| Kết luận toàn diện khi 10/88 case chưa chạy | Sai nghiệm thu | Tách SPECIFIED/NOT_RUN/PASS, coverage |

## 14. Ma trận truy vết nguồn → nhóm kiểm tra → QA tái sử dụng

| Điều khoản/Phụ lục | Kiểm tra chính | QA gốc liên quan (không phải bằng chứng PASS) |
|---|---|---|
| HD05 II.I.1–5, PL1 | Phần đầu, số/ký hiệu, trích yếu, dấu | `T01`, `T02`, `T04`, `TEST-G31`, `TEST-G33`, `G47–G51` |
| HD05 II.I.6.1–6.2 | Căn cứ, nội dung, cấp bố cục | `T05`, `T06`, `T19`, `TEST-G35`, `G52` |
| HD05 II.I.7–9 | Chữ ký, con dấu, nơi nhận | `T03`, `T11`, `T22`, `TEST-G34`, `TEST-G36`, `G57–G59` |
| HD05 II.II.1 | Phụ lục | `T08`, `T09`, `T23`, `G60–G62` |
| HD05 II.II.2–6 | Khẩn, mật, dự thảo, liên hệ | `T10`, `T25`, `G55`, `G56`, `G70`, `G88` |
| HD05 II.III/PL3, QĐ399 Điều 17 | Bản sao giấy/điện tử | `T12`, `T13`, `TEST-G38`, `G63`, `G64`, `G76`, `G77`, `G83` |
| HD05 II.IV/PL2 | Khổ, lề, số trang, kiểu chữ | `T14`, `T26`, `TEST-G40`, `G62`, `G79`, `G80` |
| HD05 Phần III, QĐ399 Điều 18 | Thời gian hiệu lực, chuyển tiếp | `T15`, `T29`, `TEST-G43`, `G74` |
| Kiểm chứng OOXML/kiểm soát auto-fix | Giữ nội dung/ảnh/bảng/parts | `T07`, `T17`, `T18`, `T27`, `T28`, `G66–G69`, `G81`, `G82` |

**Tình trạng QA tại thời điểm lập V12:** có **88 tình huống được mô tả, chưa có bằng chứng trong vòng làm việc này rằng một tình huống nào đã được chạy**. Danh mục đầy đủ giữ nguyên mã ở Mục 17.

## 15. Vấn đề còn mở, quyết định nghiệm thu và khuyến nghị

| Mã vấn đề | Nội dung chưa khép | Bằng chứng cần bổ sung | Kết quả xử lý hiện tại |
|---|---|---|---|
| O01 | Ký hiệu Công văn 5053 trong QĐ399 và HD05 không đồng nhất | Văn bản gốc/cơ quan có thẩm quyền làm rõ alias | `OPEN` — giữ nguyên 2 ID |
| O02 | Hiệu lực chi tiết Hướng dẫn 03 về chữ ký số, các sửa đổi tiếp theo | Bản nguồn chính thức và quan hệ amendment | `OPEN` — audit only |
| O03 | Toàn bộ canonical rules HD05 đã được verified/import trong runtime hay chưa | Audit registry/rule IDs/commit hiện hành và log | `OPEN` |
| O04 | Parser DOCX có bảo toàn objects/AlternateContent/round-trip hay không | Fixture thật, manifest XML/rels, diff sau mở lại | `NOT_TESTED` |
| O05 | Kiểm chứng layout giấy thật/đường kẻ/dấu stamp | Render hoặc vật mang tin gốc có quy trình xác minh | `NOT_EVALUATED` ở trường hợp chỉ có text |
| O06 | Điểm nào trong 88 QA đã có test PASS thực sự | Báo cáo máy chạy, input/output, exit code, evidence | `NOT_TESTED` |
| O07 | Đối tượng/loại văn bản cụ thể của từng tổ chức | Thẩm quyền, hồ sơ lập cơ quan, xác nhận người dùng | `NEEDS_REVIEW` nếu thiếu |
| O08 | Điều kiện xử lý tài liệu mật hoặc đã ký | Quyền/thiết bị/môi trường, PKI, yêu cầu an toàn | `NO-GO` auto-fix khi chưa chứng minh |

**Kết luận nghiệm thu V12:** **GO** ở phạm vi **tài liệu đặc tả phục vụ thẩm định** (nguồn chính được đối chiếu, quy tắc có truy vết, QA giữ nguyên mã). **NO-GO** đối với phê duyệt tất cả rule thành `verified`, tuyên bố hệ thống định dạng HD05 hoàn thành, hoặc cho phép sửa DOCX production khi chưa có bằng chứng O03–O08.

## 16. Hợp nhất lịch sử V1–V11, điểm đính chính và nhật ký V12

### 16.1. Những chỗ phải lấy kết luận V12 thay vì lặp câu cũ

| Chủ đề | Cách hiểu thiếu chính xác trong lịch sử | Cách đọc chuẩn V12 |
|---|---|---|
| “Bao nhiêu phần” | 3/4/6 là số phần văn bản bắt buộc | Đó chỉ là nhóm xử lý dữ liệu; điều 15 có 9 thành phần |
| Một khuôn định dạng chung | Tất cả 14 pt hoặc mọi trích yếu đều đậm | PL2 thay đổi theo trường; công văn trích yếu 12 nghiêng |
| “Nơi nhận” chỉ có ở cuối | `Kính gửi`/`Kính trình` bị nhập body | Vai trò nơi nhận có thể ở đầu, nhưng ô và cách trình bày khác |
| Mọi đường ngang giống nhau | `-----` = border và đường copy = contact | Phân biệt text separator, nét liền, vị trí và chủ sở hữu |
| Chữ ký/bản sao | Ảnh con dấu hoặc text ký = chứng thực số | Cần authority/PKI/vật mang tin phù hợp |
| Đủ 88 QA nghĩa là hoàn thành | QA mô tả = QA PASS | Chỉ 88 ca SPECIFIED; phải chạy có evidence |
| Source registry đã có | Có khai báo nguồn = đã triển khai đủ rule | Cần audit `verified` và coverage riêng |
| Chỉ dẫn pháp lý mâu thuẫn | Tự gộp 5053 vì gần giống tên | Lưu cả hai chuỗi, chờ đối chiếu có thẩm quyền |

### 16.2. Bản đồ hợp nhất phạm vi V1–V11

| Nội dung | Các mục V11 lưu làm lịch sử | V12 tương ứng |
|---|---|---|
| Khái niệm và bố cục | 1–8, 16–18, 49, 64–67, 83–85, 93, 101 | 1, 3, 4, 6, 8 |
| Quy tắc định dạng, phụ lục, bản sao | 9–10, 19, 25, 33–36, 40, 48, 55–58, 65, 74–76, 84, 94, 100 | 5, 6, 7, 9 |
| Nguồn và hiệu lực | 2, 15, 23–24, 32, 47, 53–54, 61, 71, 76, 88, 91–92, 99 | 2, 4, 15 |
| DOCX, QA, auto-fix | 11–14, 20–22, 26–31, 37–39, 41–43, 50–52, 59–60, 68–70, 77–78, 86–87, 95–96, 102 | 8–14, 17 |
| Audit, lịch sử sửa đổi | 44–46, 62–63, 72–73, 79–82, 89–90, 97–98, 103–104 | 15–16 |

**Nhật ký phiên V12:**

1. Hợp nhất phần quy tắc đang áp dụng thành tài liệu độc lập thay vì nối thêm mục 105+.
2. Đối chiếu lại trực tiếp PDF HD05 trang 30–36: ô PL1, bảng PL2 và dấu bản sao PL3; hiệu chỉnh bảng kiểu chữ.
3. Gắn trường và giá trị với nguồn theo mục/điểm/phụ lục, phân biệt điều khoản bắt buộc và mô hình ứng dụng.
4. Giữ nguyên mã 88 QA từ V11, tạo danh mục tái sử dụng duy nhất ở Mục 17.
5. Tách rõ `SOURCE_CONFIRMED` so với runtime `VERIFIED` và kết quả test `PASS`.
6. Kiểm tra HEAD repo để tránh suy luận quá mức về mức độ triển khai; không ghi sửa/commit lên repo.
7. Liệt kê tồn đọng với trạng thái, bằng chứng cần có và điều kiện Go/No-Go.

---

## 17. Danh mục gốc 88 tình huống QA — giữ mã ổn định

Các tình huống dưới đây **là đặc tả tái sử dụng lấy từ V11**, không phải 88 kết quả PASS. Không tạo thêm fixture hoặc test mới chỉ để đổi tên mã. Khi triển khai, dùng assertion/fixture đang có và chỉ bổ sung kiểm tra cho lỗ hổng thật sự.

| Mã giữ nguyên | Đầu vào hoặc tình huống | Kết quả kỳ vọng |
|---|---|---|
| `T01` | Văn bản có tên loại, có đủ tiêu đề/địa danh/trích yếu | Các trường `front` đúng; `body` bắt đầu sau ranh giới |
| `T02` | Công văn có trích yếu dưới số/ký hiệu và `Kính gửi` | Không sinh tên loại ở giữa; không đưa `Kính gửi` vào body |
| `T03` | Tờ trình có `Kính trình` và `Nơi nhận` | Có `front.addressee` và `end.recipients` riêng |
| `T04` | Văn bản với dấu `*`, đường kẻ liền, `-----` | Phân biệt từng separator, không tạo/xóa nhầm |
| `T05` | Văn bản có nhiều căn cứ mở đầu bằng `-` | Mọi căn cứ thuộc body và giữ nguyên dấu/chấm câu |
| `T06` | Có `Phần`, `Chương`, `Mục`, `Điều`, `Khoản`, `Điểm` | Giữ cây cấp bậc, không nhầm với vùng thể thức |
| `T07` | Có bảng và ảnh ngay trước chữ ký | Bảng/ảnh thuộc body, thứ tự và object refs không đổi |
| `T08` | Có hai phụ lục với bảng/ảnh riêng | `appendices.length = 2`, có số/tiêu đề/chỉ dẫn riêng |
| `T09` | Một phụ lục đơn lẻ, không số thứ tự | Không tự sinh số nếu nguồn không yêu cầu |
| `T10` | Có dấu khẩn, chỉ dẫn dự thảo/phạm vi lưu hành | Overlay đúng vị trí, không lẫn body |
| `T11` | Có nhiều chữ ký/xác nhận | Giữ đủ các khối, thứ tự và seal refs |
| `T12` | Bản sao giấy có đường kẻ và dấu **“Bản sao văn bản”** | Không nhầm dấu bản sao với dấu `*`; giữ các trường mẫu 80×50 mm, màu đỏ, tên dấu 13 pt đậm và ký hiệu `BS` (khi có chứng cứ); không trộn `copy_metadata` vào bản chính. Nếu chỉ có DOCX text, trạng thái kiểm tra mực/kích thước dấu là `NOT_EVALUATED` |
| `T13` | Bản sao điện tử/đã ký số | Không tự sửa làm mất hiệu lực chữ ký |
| `T14` | DOCX nhiều section, in hai mặt và có trang ngang | Lề/khổ/hướng trang theo đúng section |
| `T15` | Văn bản lịch sử trước ngày HD05 có hiệu lực | Resolver không áp sai luật theo ngày hiện tại |
| `T16` | Parser không phân loại được trường | Đưa vào `unclassified[]`, báo `NEEDS_REVIEW` |
| `T17` | Chọn sửa duy nhất một rule | Chỉ target được sửa; nội dung/thuộc tính ngoài scope không đổi |
| `T18` | Xuất và mở lại DOCX | Round-trip content/order/object integrity PASS |
| `T19` | Chữ `Nơi nhận` xuất hiện trong đoạn văn thông thường | Không nhầm thành vùng `end` |
| `T20` | Dấu sao trong bảng nội dung, dấu gạch `-----` trong phụ lục | Không sinh ranh giới giả |
| `T21` | Công văn không có separator 5 gạch | Không tự thêm năm dấu gạch vào công văn |
| `T22` | Tờ trình có `Kính trình` nhiều dòng và nơi nhận nhiều đối tượng | Các nơi nhận đầu/cuối tách đúng, không mất dấu câu |
| `T23` | Một tài liệu có bảng phụ lục ngang nhưng không section riêng | Phụ lục và bảng vẫn có owner đúng |
| `T24` | Hai phần nội dung liền nhau nhưng một phần nằm trong bảng | Không đảo thứ tự đoạn/bảng |
| `T25` | Dấu thông tin liên hệ và dấu bản sao đều dùng đường kẻ toàn vùng | Nhận diện khác nhau theo bối cảnh |
| `T26` | Trang đầu không hiển thị số, trang sau có field PAGE | Không sửa field/lề sai và không đánh dấu lỗi giả |
| `T27` | DOCX có track changes/comment/hyperlink/footnote | Không mất phần được bảo toàn ngoài phạm vi |
| `T28` | DOCX chứa chữ ký số/được bảo vệ | Chỉ kiểm tra hoặc từ chối tự sửa; tình trạng tài liệu không bị thay đổi |
| `T29` | Tài liệu gốc ban hành 10/05/2026 | Không mặc định áp HD05 có hiệu lực 27/05/2026 |
| `T30` | Block có hai role cạnh tranh, cùng mức tin cậy | Giữ nguyên, vào `unclassified[]`, đòi duyệt |
| `TEST-G31` | Đường kẻ dưới tiêu đề Đảng được tạo bằng border, không phải chuỗi ký tự | Nhận đúng đối tượng; không tạo `-----` giả |
| `TEST-G32` | Đoạn chứa `*` nằm trong ô bảng giữa body | Không gán vào dấu phân cách tên cơ quan |
| `TEST-G33` | Trích yếu công văn 12pt nghiêng, xuất HTML làm mất cỡ chữ | Kiểm từ OOXML hiệu dụng; không báo PASS chỉ nhờ HTML |
| `TEST-G34` | Tờ trình có `Kính trình` ở ô 9b và danh sách “Như trên” ở cuối | Hai vùng đầu/cuối giữ danh sách/nhãn độc lập |
| `TEST-G35` | `- Căn cứ...;` nằm sát trích yếu và 5 dấu gạch | Giữ là nội dung vùng 6, không chuyển vào `front` |
| `TEST-G36` | Biên bản có nhiều người ký, chức vụ khác nhau và dấu ở cột riêng | Không làm lẫn người ký hoặc mất đối tượng neo |
| `TEST-G37` | Phụ lục dài nhiều trang, 2 phụ lục có page number độc lập | Mỗi phụ lục có owner và phạm vi số trang đúng |
| `TEST-G38` | Có hai đường ngang toàn vùng: trên contact và trên thể thức sao | Không nhầm hai đường và không cắt sai body |
| `TEST-G39` | DOCX có chữ ký số, yêu cầu sửa lề | Audit-only/NO-GO tự sửa, không làm hỏng file |
| `TEST-G40` | Trang đầu ẩn số; in hai mặt; section bảng ngang ở giữa | Không suy một mẫu lề/số trang cho toàn tài liệu |
| `TEST-G41` | Có track changes, footnotes, comments, hyperlink | Đối tượng ngoài phạm vi sửa còn nguyên theo kiểm tra có chọn lọc |
| `TEST-G42` | Có bảng lồng nhau và image anchor bên trong phụ lục | Không nhân đôi/mất bảng ảnh khi round-trip |
| `TEST-G43` | Văn bản cấp ủy trước 27/05/2026 nhưng tệp được cập nhật năm 2026 | Không định tuyến HD05 chỉ dựa file-modified-date |
| `TEST-G44` | Văn bản chưa xác định `regime` với metadata không đầy đủ | `NEEDS_CLASSIFICATION`; không auto-fix |
| `TEST-G45` | Hai field tranh chấp cùng một paragraph nguồn | `unclassified[]`, không gán hai owner |
| `TEST-G46` | A5 thuộc trường hợp giấy tờ hành chính được phép | Không báo lỗi khổ A4 giả do dùng template chung |
| `G47` | Số `05-...`, số `5-...` | Phát hiện thiếu 0; không tự sửa số đã đăng ký |
| `G48` | Ký hiệu `CTr`, `TTr`, hai loại `QĐ` | Không sửa sai chữ viết tắt hợp lệ |
| `G49` | Công văn có 5b dài nhiều dòng | Toàn bộ trích yếu ở front; không tràn vào body |
| `G50` | Tờ trình có `Kính trình` nhiều đơn vị | Vùng 9b đầy đủ và liên kết `Như trên` |
| `G51` | Căn cứ đầu body và dấu `-----` phía trước | Giữ đúng hai loại dấu, chủ sở hữu riêng |
| `G52` | Nhiều căn cứ với `;` và `.` cuối | Chỉ cảnh báo lỗi ký tự; không viết lại khi lệnh format |
| `G53` | Địa danh có tiền tố `Phường`/`Thành phố` | Không tự chuẩn hóa sai địa danh theo điều kiện tên riêng |
| `G54` | Ngày `09` và tháng `02` | Xác định logic theo nguyên văn S1 I.4.2.2 |
| `G55` | Trang đầu có dòng chỉ dẫn lưu hành ở góc phải | Overlay không bị gom vào metadata tên cơ quan |
| `G56` | Chỉ dẫn không phổ biến xuất hiện trang cuối | Không chuyển lên trang đầu do nhầm mặc định |
| `G57` | Nhiều khối ký biên bản | Không mất khối trái; đúng thứ tự/role |
| `G58` | Đại hội không có con dấu, có xác nhận chữ ký | Không coi attestation là phụ lục hoặc body |
| `G59` | Liên cơ quan, nhiều người ký và dấu | Bảo toàn đầy đủ `signatures[]` |
| `G60` | Phụ lục có tên nhưng không được đánh số (chỉ 1 PL) | Không cưỡng bức thêm “Phụ lục 1” |
| `G61` | Từ 2 phụ lục, đánh số riêng, số trang riêng | Tách chính xác `appendices[0]`, `[1]` |
| `G62` | Phụ lục nhiều trang, bảng ngang | Giữ orientation và page number theo phụ lục |
| `G63` | Bản sao giấy có stamp 80×50 và ký sao | Nhận riêng `copy_metadata`; kiểm tra các trường mẫu dấu (màu đỏ, tiêu đề 13 pt đậm, ký hiệu `BS`) khi có chứng cứ; không sửa bản chính, không báo PASS thay cho kiểm tra mực/kích thước nếu không đủ khả năng |
| `G64` | Bản sao điện tử có hai vị trí chữ ký số đầu trang | Ô 14/15 không lẫn tiêu đề Đảng/địa danh |
| `G65` | Bản scan chỉ có ảnh trang | Không bịa semantic roles từ text rỗng; cần xem xét riêng |
| `G66` | Tệp chứa XML signature/protection | Bảo toàn gói; từ chối auto-fix |
| `G67` | Word có `mc:AlternateContent` và fallback | Không mất một nhánh vì HTML conversion |
| `G68` | Word có section odd/even/first headers | Chỉ patch section thuộc phạm vi |
| `G69` | Textbox/footnote/comment/track changes | Manifest đầy đủ; không mất silently |
| `G70` | Document mật/không xác định quyền | Không upload/ghi log nội dung, chuyển kiểm tra bảo mật |
| `G71` | Giấy giới thiệu A5/mẫu in sẵn | Router tới `party.admin_paper`, không cưỡng ép A4 hoặc các đề mục như quyết định |
| `G72` | Thư công của người đứng đầu cơ quan | Không mặc định kết luận là văn bản chính thức mang toàn bộ 9 trường |
| `G73` | Tệp văn bản chuyên ngành do cơ quan Đảng giữ | `NEEDS_CLASSIFICATION` hoặc bộ quy tắc chuyên ngành, không auto HD05 |
| `G74` | Văn bản ký ngày 01/03/2026 | Không áp HD05 hồi tố; kiểm resolver nguồn theo thời gian |
| `G75` | Công văn và tờ trình cùng ghi người nhận đầu | Đúng role tương ứng: công văn `5a/Kính gửi`, tờ trình `9b/Kính trình` |
| `G76` | Bản sao giấy không có nơi nhận bản sao | Phát hiện thiếu trường bản sao có thể áp dụng; không tự bịa nơi nhận |
| `G77` | Bản sao điện tử thiếu chữ ký số cá nhân hoặc cơ quan sao | Không coi ảnh PNG trong trang là bằng chứng ký số |
| `G78` | Giấy tờ có header nhưng nội dung không có đủ nhan đề/trích yếu thông thường | Không tự thêm thành phần vào mẫu khi chưa có căn cứ |
| `G79` | Lề/tài liệu in hai mặt và hai section có trang ngang | Không lan thay đổi lề/hướng trang vào section không được chọn |
| `G80` | Văn bản dùng một style chung cho body và nơi nhận | Patch field được chọn không làm thay style vùng kia |
| `G81` | Phụ lục có bảng lồng, ảnh nổi và số trang độc lập | Manifest đối tượng và ranh giới phụ lục không mất/trùng |
| `G82` | Tài liệu có chữ ký số/protection nhưng yêu cầu sửa số trang | Từ chối tự sửa, giữ nguyên source và báo lý do |
| `G83` | Có bản sao giấy, parser đọc nhầm dấu mẫu 80×50 thành `*` dưới tên cơ quan | Đúng vùng copy; không xóa/di chuyển dấu; physical stamp validation `NOT_EVALUATED` nếu chỉ XML |
| `G84` | Sổ cấp số cho đại hội có hai tên loại khác nhau, số nối tiếp | Không chia thành 2 sequence nếu đã có sổ hợp lệ |
| `G85` | Văn bản mật và không mật cùng tên loại trong một nhiệm kỳ | Không yêu cầu dãy số bí mật tách biệt; không đọc sổ khi chưa có quyền |
| `G86` | Cấp phó được phân công phụ trách khi chưa có cấp trưởng | Không tự thêm `K/T`; giữ đúng khối ký và yêu cầu căn cứ |
| `G87` | HD03 được trích dẫn từ HD05 nhưng resolver không có hiệu lực từng điều | `SOURCE_LOCATED_STATUS_OPEN` / `NOT_EVALUATED`, không mở rule ký số tự động |
| `G88` | Chỉ dẫn “Không phổ biến trên phương tiện thông tin đại chúng” ở cuối tài liệu | Không báo lỗi vì không đặt ở đầu; không gán vào nội dung body |

**Số lượng:** T01–T30 = 30; TEST-G31–TEST-G46 = 16; G47–G88 = 42; tổng **88**. Mọi mã `G01`, `G31`, `G46` xuất hiện ở lịch sử chỉ là tham chiếu khác hệ, không tạo kịch bản bổ sung.
