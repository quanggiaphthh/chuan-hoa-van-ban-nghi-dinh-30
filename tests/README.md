# Tests

GĐ0 tập trung kiểm thử contract và routing, chưa kiểm thử production DOCX engine.

## Nhóm test bắt buộc

1. **Schema** — source/rule/rule-pack/profile/context/document-model/result/patch hợp lệ hoặc bị từ chối đúng kỳ vọng.
2. **Routing** — phân biệt administrative / party / unknown.
3. **Temporal** — rule chỉ áp trong khoảng hiệu lực phù hợp.
4. **Applicability** — rule theo document type/component/section role.
5. **Conflict** — organizational profile, official guidance hoặc official example không override mandatory legal rule.
6. **Coverage** — `NOT_EVALUATED` phải làm giảm coverage.
7. **Safety** — signed/protected/unsupported document không được auto-fix trái policy.

## Fixtures tối thiểu dự kiến

```text
tests/
  fixtures/
    schema-valid/
    schema-invalid/
    routing/
    temporal/
    applicability/
    safety/
```

## Invariants

- Mandatory rule phải có source locator.
- Verified rule phải tham chiếu nguồn đã kiểm chứng.
- Expired rule không áp sau `effective_to`.
- Profile nội bộ không được nới rộng mandatory legal constraint.
- Rule pack không được chứa bản sao constraint.
- Unknown regime tắt guarded structural auto-fix.
- `PROHIBITED` patch không bao giờ được thi hành.
- Historical audit dùng law state theo thời điểm cần xét.
