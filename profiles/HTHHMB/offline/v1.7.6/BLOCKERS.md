# Các blocker còn tồn tại

## B01 — UNVERIFIED

- **Trường:** `HD05.N.BANG.O.CO_CHU / DAU_BANG.CO_CHU`
- **Lý do:** User-chosen 13 pt but HD05 Appendix 2 body 14–15 pt; no table-wide exemption verified
- **Cần:** Cơ sở HD05 riêng cho mọi ô bảng hoặc quy trình phê duyệt ngoại lệ; không sửa 13 pt đã chốt

## B02 — UNVERIFIED

- **Trường:** `HD05.N.BANG.O.GIAN_DONG`
- **Lý do:** Single per profile versus HD05 body Exactly 18–22 pt; table cell scope unclear
- **Cần:** Văn bản chính thức xác nhận áp dụng Single cho bảng của hệ HD05 / phê duyệt ngoại lệ

## B03 — BLOCKED

- **Trường:** `ND30/HD05.D.CQ.BAN_HANH; CAP_TREN`
- **Lý do:** Tên cấp Đảng và quan hệ trực thuộc còn khác giữa profile và nguồn công khai
- **Cần:** Quyết định/biên bản tổ chức, văn bản Đảng ban hành còn hiệu lực xác nhận tên

## B04 — BLOCKED

- **Trường:** `ND30/HD05.D.SK.KY_HIEU_CO_QUAN`
- **Lý do:** HTMB, ĐU, ĐUHTMB chưa có văn bản mẫu để khẳng định mã văn thư chính thức
- **Cần:** Bản văn bản phát hành hợp lệ hoặc quy chế mã ký hiệu

## B05 — BLOCKED

- **Trường:** `ND30/HD05.X.LIEN_HE.*`
- **Lý do:** Liên hệ website có thể kiểm tra nhưng thời điểm giá trị, fax khác biệt; chính sách ẩn/hiện tùy văn bản
- **Cần:** Danh mục liên hệ Công ty phê duyệt + quyết định hiển thị chân trang nếu cần

## B06 — BLOCKED

- **Trường:** `ND30/HD05.C.KY.* ; DAU.*`
- **Lý do:** Không có dữ liệu danh tính người ký, thẩm quyền, chữ ký/con dấu; nằm ngoài phạm vi bản dự thảo
- **Cần:** Văn bản cụ thể và quyền hạn ký tương ứng

## B07 — BLOCKED

- **Trường:** `ND30/HD05.X.MAT.* / KHAN.*`
- **Lý do:** Không thể tự suy mức mật/khẩn từ hình thức hoặc nội dung
- **Cần:** Chỉ đạo phân loại/cấp có thẩm quyền, theo văn bản

## B08 — UNVERIFIED

- **Trường:** `ND30/HD05.N.BANG.* 6pt/1.5mm/0.5pt/100%`
- **Lý do:** Đã chốt lựa chọn profile trong hội thoại, không phải trị số pháp lý được HD05/NĐ30 quy định riêng
- **Cần:** Không cần người dùng chốt lại; chỉ cần QA trên văn bản mẫu để chứng minh phù hợp

## B09 — BLOCKED

- **Trường:** `ALL_RULES`
- **Lý do:** Chưa có quyết định phê duyệt nội bộ chính thức; CW không được gán số hiệu giả
- **Cần:** Văn bản phê duyệt có số thật nếu cần sử dụng trạng thái CORPORATE_APPROVED

## B10 — UNVERIFIED

- **Trường:** `TQA_NR`
- **Lý do:** Không có font-shaping thật/đo độ rộng/chuyên gia đánh giá; ngưỡng đẹp chưa hiệu chỉnh
- **Cần:** DOCX benchmark + font thực, đo cấu trúc và nhãn đánh giá độc lập

## B11 — UNVERIFIED

- **Trường:** `G/section/orientation`
- **Lý do:** Bảng rộng có thể cần section landscape, nhưng chưa chứng minh bố cục DOCX thực tế
- **Cần:** DOCX cụ thể và phê duyệt đổi hướng trang

## B12 — UNVERIFIED

- **Trường:** `N.CAN_CU and numbering`
- **Lý do:** Dạng căn cứ/đánh số phải theo đúng loại văn bản; chưa phân theo 209 loại template
- **Cần:** Loại văn bản thực và mẫu tương ứng nếu làm QA chi tiết, không phát triển template hiện nay

## B13 — UNVERIFIED

- **Trường:** `D/X/G sources`
- **Lý do:** Nhiều preset kế thừa có URL cấp văn bản nhưng chưa có điều khoản xác minh ở cấp thuộc tính
- **Cần:** Đối chiếu chi tiết phụ lục/loại văn bản và ghi locator riêng

## B14 — BLOCKED

- **Trường:** `TQA test rendered appearance`
- **Lý do:** Không có tài liệu DOCX gốc và kết quả render hiện tại
- **Cần:** Mẫu thật để kiểm tra sau này; offline structural QA vẫn có thể hoàn tất

