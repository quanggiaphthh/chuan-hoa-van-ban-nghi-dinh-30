# HTHHMB v1.7.6 — Biên bản đối chiếu phát hành trên GitHub (29/10/2026)

**Repository:** `quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30` · **Branch:** `main`  
**Loại kiểm tra:** so sánh danh tính Git blob các tệp đã công bố với tệp nguồn trong gói v1.7.6 được kiểm tra tại phiên làm việc. Đây là QA danh tính tệp và cấu trúc, **không phải nghiệm thu pháp lý hay hiển thị DOCX**.

## Kết quả

- **PASS — 11/11 tệp nguồn có mặt trên GitHub có Git blob SHA trùng khớp**, kể cả workbook Excel nhị phân nguyên bytes.
- **PASS — nguồn ZIP nội bộ kiểm tra CRC:** `BO_RULE_OFFLINE_HTHHMB_v1.7.6_FULL.zip`, 25 thành phần, SHA-256 `16990a8dc696bcae702f175279c95e8e05348856c8d938f30ee449cef52c9861`.
- **PASS — chạy kiểm tra cấu trúc trên ZIP nguồn:** 41 worksheet, 26 worksheet gốc bảo toàn, 4.372 công thức, 165 trường cha, 132 rule bảng, 868 rule C/N/G/X bổ sung.
- **NOT PUBLISHED — ZIP đầy đủ chưa nằm trong repository này**, nên không được tuyên bố GitHub chứa cả 25 thành phần ZIP; riêng các tệp Markdown trong `rules/`, dữ liệu gốc v1.7.4/v1.7.5 nhúng trong ZIP cũng chưa được công bố dưới dạng tệp rời.
- **UNVERIFIED / BLOCKED —** ngoại lệ HD05 của chữ 13 pt và Single trong ô bảng; thông tin cơ quan/Đảng còn thiếu chứng cứ; kiểm định hiển thị DOCX và hiệu chỉnh điểm thẩm mỹ TQA-NR.

## Bảng mã băm đối chiếu

| Tệp trong `profiles/HTHHMB/offline/v1.7.6/` | Git blob SHA nguồn = GitHub |
|---|---|
| `AUDIT_BANG_132.csv` | `181a290879a4239b109f1453eb21e91f3a58ad0f` |
| `BLOCKERS.md` | `3de1c43ae7795f48bb53684f03e1025be89f9e8c` |
| `BO_RULE_PROFILE_HTHHMB_v1.7.6_OFFLINE_FULL.xlsx` | `210f397acb507cdd2acc06cf221a0e94abf15faf` |
| `MA_TRAN_165.csv` | `6b5fd792ce8c9159b677e2ae983668511db5ba3a` |
| `NGHIEN_CUU_TQA_NR_V17_2_SOURCE.md` | `298abd16dad16e52c26bf1169b75185906810bfa` |
| `QA_CHECKLIST.md` | `207bbcc1c23bc466ac530d050463672d1254574a` |
| `READ_FIRST.md` | `6c64d5f08d6297440f09870c3d9ca3e8715cccb7` |
| `RULE_ALL_ATTRIBUTES.csv` | `6a5c321a77de3946dd06554c5d8df84831ad71ae` |
| `TQA_NR.md` | `e7e2038d78d43fd9e4c899e2fee2a96d0d3e41f8` |
| `VERIFY_PACKAGE.py` | `26932763e2828270a75f376dc8522fbe9198f592` |
| `manifest.json` | `92744e937602191a35f54ca47af423dd456efc72` |

## Tái kiểm tra

1. Tải bản phát hành từ GitHub; chạy `git hash-object <path-to-file>` và so sánh bảng trên. Kiểm tra `manifest.json` và `RELEASE_README.md` để biết phạm vi.
2. Nếu có **ZIP nguồn đầy đủ**, chạy `python VERIFY_PACKAGE.py /path/to/BO_RULE_OFFLINE_HTHHMB_v1.7.6_FULL.zip`. Không áp dụng bước này cho riêng thư mục GitHub thiếu ZIP.
3. Khi sử dụng rule, đọc `READ_FIRST.md`, `BLOCKERS.md`, `TQA_NR.md`. Phân biệt pháp lý đã xác minh, giá trị profile đã chốt, preset, và UNVERIFIED/BLOCKED.

**Giới hạn thay đổi:** biên bản này chỉ bổ sung bằng chứng kiểm kê cho bản đã được commit. Không ghi đè schema, rule packs, template 209 loại hoặc bất kỳ mã formatter nào.
