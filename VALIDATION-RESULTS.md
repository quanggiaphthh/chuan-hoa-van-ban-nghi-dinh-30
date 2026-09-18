# Validation Results

## Trạng thái đánh giá

`status` và `severity` là hai khái niệm độc lập.

### Status

- `PASS`
- `FAIL`
- `NOT_APPLICABLE`
- `NOT_EVALUATED`
- `NEEDS_REVIEW`

### Severity

- `LEGAL_ERROR`
- `LEGAL_WARNING`
- `PROFILE_MISMATCH`
- `QUALITY_WARNING`
- `INFORMATION`

Ví dụ một rule có thể `FAIL + PROFILE_MISMATCH`: tài liệu vẫn nằm trong biên pháp luật nhưng không đúng profile nội bộ.

## Kết quả rule

Mỗi result nên có:

```yaml
rule_id:
status:
severity:
target:
expected:
actual:
source:
message:
evidence:
autofix:
```

## Coverage

Báo cáo tổng hợp bắt buộc ghi coverage:

```yaml
coverage:
  applicable_rules: 143
  evaluated_rules: 121
  not_evaluated_rules: 22
summary:
  passed: 114
  failed: 7
  needs_review: 0
```

Không được kết luận “hoàn toàn tuân thủ” khi vẫn có rule applicable nhưng `NOT_EVALUATED`.

## Invariants

- `NOT_EVALUATED` làm giảm coverage.
- `UNKNOWN` regime không được tạo kết luận compliance toàn phần.
- Kết quả phải trỏ về `rule_id` và evidence.
- Mọi auto-fix phải liên kết được về validation result/rule tương ứng.
