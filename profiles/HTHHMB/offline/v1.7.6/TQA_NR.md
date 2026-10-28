# G0–G5 TQA-NR (chưa hiệu chỉnh điểm thẩm mỹ)

| Gate | Loại | Mô tả | Bằng chứng | Kết luận |
|---|---|---|---|---|
|G0|HARD_GATE|Legal scope, provenance, text integrity, locks, 13pt TNR effective in table|STRUCTURE_FROM_WORKBOOK|BLOCKED_IF_MISMATCH|
|G1|HARD_GATE|Preserve row/column associations, repeated hierarchical headers, merges, totals, meaning of source labels|RULE_INFERENCE_NOT_MEASURED|REVIEW|
|G2|RISK_GATE|usable width from section/container; trHeight and paragraph exact line spacing, margin cell, footnotes/nested tables|NO_FONT_MEASUREMENT|UNKNOWN|
|G3|PROXY|Comparison/lookup/summary/action/compliance, number alignment and wrapping|HEURISTIC_ONLY|NOT_CALIBRATED|
|G4|PROXY|Padding, alignment, border hierarchy, repetition, whitespace consistency|RULE_BASED_INFERENCE|NOT_CALIBRATED|
|G5|PREFERENCE|When score difference ambiguous, preserve baseline and explain review|PROFILE_POLICY|CHECK_ONLY|

G0 và ngữ nghĩa G1 có tính gate; G2 không được tự nhận đo font; G3/G4 là proxy; G5 ưu tiên baseline.
