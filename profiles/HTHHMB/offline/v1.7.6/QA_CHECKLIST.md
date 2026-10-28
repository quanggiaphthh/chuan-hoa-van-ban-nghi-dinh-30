# Checklist nghiệm thu v1.7.6

| Công việc | Trạng thái | Kết quả / giới hạn |
|---|---|---|
| Giữ nguồn v1.7.4/v1.7.5 | **PASS** | Bảo toàn file và toàn bộ sheet gốc; checksum manifest |
| Độ phủ 132 bảng | **PASS** | 132/132 mã duy nhất; kiểm tra alias, 6pt, role, xung đột |
| D — Phần đầu | **PASS** | Giữ 434 rule v1.7.5; chưa xác minh mọi thuộc tính |
| C — Phần cuối | **PASS** | Tách rule mới với chính sách bảo vệ chữ ký/dấu |
| N — Nội dung | **PASS** | Tách rule; 5 parent bảng mỗi hệ định tuyến bảng 132 |
| G — Định dạng chung | **PASS** | Tách rule và gắn nguồn HD05/NĐ30 có kiểm chứng |
| X — Có điều kiện | **PASS** | Tách rule và không tự phát sinh các dữ liệu đặc biệt |
| Profile ND30/HD05 | **PASS** | Bảo toàn preset, tách legal/profile/source; không tự phê duyệt |
| Mã ký hiệu/tên Đảng/liên hệ | **BLOCKED** | Cần chứng cứ tổ chức hoặc xác nhận hiển thị |
| HD05 13pt + Single ô bảng | **UNVERIFIED** | Đã chốt profile, chưa chứng minh ngoại lệ pháp lý |
| TQA-NR điểm thẩm mỹ | **UNVERIFIED** | Chỉ proxy; chưa có nhãn chuyên gia hiệu chỉnh |
| DOCX Word render/QA thực tế | **BLOCKED** | Không có file DOCX và phép đo font |
| 209 template hoặc formatter | **PASS** | Không tạo/chỉnh 209 template và không phát triển formatter |

Danh sách đầy đủ nguyên nhân và dữ liệu cần bổ sung nằm trong `BLOCKERS.md` và Excel `BLOCKERS_176`.
