# HƯỚNG DẪN CHO CHATGPT — Profile HTHHMB offline v1.7.6

## 1. Tính hiệu lực và nguồn gốc

- Bản nền: **v1.7.4** (profile + chốt bảng), kế thừa **v1.7.5** (434 rule phần đầu D). Bản v1.7.6 chỉ **BỔ SUNG** lớp kiểm kê, phân rã C/N/G/X, QA; không ghi đè các sheet trước.
- NĐ30: https://congbao.chinhphu.vn/van-ban/nghi-dinh-so-30-2020-nd-cp-30858/30246.htm
- HD05 ngày 27/5/2026: https://tulieuvankien.dangcongsan.vn/upload/2006988/20260529/HD_05_VPTW_e1550.pdf

## 2. Thuật toán đọc offline (không phải chương trình formatter)

1. Chọn **hệ NĐ30 hoặc HD05** theo chính chủ thể ban hành. HD05 không áp cho doanh nghiệp chỉ vì có từ “Đảng ủy” trong bối cảnh.
2. Xác định **loại văn bản**, phần D/N/C/G/X; không đồng thời dùng hai nhánh loại trừ (như công văn và văn bản có tên loại).
3. Định vị trường cha trong `MA_TRAN_165.csv`; phần D xem `DAU_*` workbook gốc; phần C/N/G/X xem các file `rules/RULE_[GROUP]_[SYSTEM].md`; bảng xem `rules/BANG_132_AUDIT.md`.
4. Áp dụng ưu tiên: **phạm vi và quy định đã kiểm chứng → điều kiện thực tế → profile do người dùng chốt (chỉ khi không xung đột pháp lý) → preset chưa duyệt → bảo toàn + BLOCKED**.
5. Không bịa trị số; không điền số/ký hiệu, tên tổ chức, chữ ký, đóng dấu, thông tin liên hệ khi chưa có nguồn; không thay dữ liệu bảng hoặc 209 template.
6. Khi một thuộc tính có `UNVERIFIED`/`BLOCKED`, đưa báo cáo phần còn thiếu. `CHECK_ONLY` không phải phê duyệt quyền sửa.

## 3. Quy tắc bảng trọng yếu

- Ô bảng TNR **13 pt**, header 13 pt: **USER_CONFIRMED_PROFILE**. HD05 tuân thủ pháp lý riêng **UNVERIFIED**.
- **Lặp hàng tiêu đề bắt buộc** nếu bảng qua trang. Hai mã `DAU_BANG.LAP_LAI` và `NGAT_TRANG.GIU_DAU_BANG` là **một quyết định**.
- Bốn ranh giới bảng/caption/note với mục tiêu **6 pt**: mỗi ranh giới chỉ một quy tắc; không chèn thêm 6 pt lên `spaceAfter/Before` đang có. Đây là mục tiêu logic, **không tuyên bố Word tự collapse**.
- Giãn dòng ô Single hai hệ là **profile**, không nói HD05 ban hành quy định như vậy. Giãn dòng lời văn HD05 là **Exactly 18–22 pt**, giãn đoạn >=6 pt.
- Khi `100%` và `PRESERVE_IF_VALID` mâu thuẫn, giữ bảng đã hợp lệ, không ép 100% bằng mọi giá.
- Chữ trái, đại lượng số phải, mã/STT giữa. Nền có ý nghĩa được bảo toàn. Viền 0,5 pt chỉ khi bảng có viền.
- Bảo toàn nhóm ô gộp và quan hệ nhãn–dữ liệu. Bảng dài: giữ hàng khi vừa trang, hàng quá dài phải REVIEW.

## 4. TQA-NR và mức chắc chắn

- `LEGAL_EXPLICIT_CHECKED`: đã đối chiếu đúng vị trí nguồn quy định.
- `STRUCTURE_READ_EXISTING_WORKBOOK`: dữ liệu có thực trong workbook; không đồng nghĩa đã đo Word/DOCX.
- `RULE_INFERENCE_NOT_MEASURED`: suy luận theo quy tắc; không gọi đo thực.
- `M1_MODEL_ESTIMATE`: chỉ được dùng khi có phép đo shaping font TNR thật và điều kiện đo rõ, ở giai đoạn này **không có**.
- G0/G1 là gate integrity và semantic, G2 là rủi ro hình học (UNKNOWN khi thiếu DOCX/font); G3/G4 là proxy đọc hiểu/nhất quán; G5 ưu tiên sửa tối thiểu.
- **Không có điểm đẹp đã hiệu chỉnh**, không coi nhận định LLM là nhãn chuyên gia.

## 5. Minh họa logic (không phải kiểm chứng render)

1. Caption ngoài bảng trước bảng: khoảng cách caption–bảng **6 pt tổng**, không thêm khoảng trước bảng.
2. Hai hàng header trong bảng dài: bắt buộc lặp cả hai nếu là hàng tiêu đề đầu bảng, giữ liên kết nhãn cột.
3. HD05 body: không tự ép Single như trong bảng lên lời văn; giữ Exactly 18–22 pt theo HD05.
4. Bảng có số điện thoại: căn theo mã/chuỗi, không tự coi số điện thoại là đại lượng.
5. Không có thông tin văn thư xác minh: mã cơ quan và tên Đảng ủy là BLOCKED, không tự phát hành.

## 6. QA

Xem `QA_CHECKLIST.md`, `manifest.json` và `BLOCKERS_176` trong Excel. Tất cả PASS ở đây là **PASS kiểm dữ liệu / logic cấu trúc**, KHÔNG phải PASS hiển thị DOCX hay phù hợp pháp lý toàn bộ.
