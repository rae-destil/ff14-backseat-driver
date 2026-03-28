---
name: instance-entry-validator
description: Validate candidate BackseatDriver instance hint JSON against provided source material and schema rules. Use this when a map entry or territory entry has already been drafted and needs semantic review before running the local validator or upsert tooling.
---

# Instance Entry Validator

Review a drafted `BackseatDriver` instance entry against its source material before it is applied to the dataset.

This skill is for semantic validation. It does not scrape sources and it does not perform the final deterministic file validation itself.

## Load These References First

Before validating, read:

- `instances_compressor/instances_data_schema.md`
- `AGENTS.md`

Use the schema document as the contract for valid output shape and hint-mode rules.

## Expected Inputs

You should have:

- the original source material used to create the entry
- the candidate JSON entry
- whether the candidate is:
  - one map entry, or
  - one territory entry
- territory ID and map ID context if available

If the candidate was derived from existing repo data, you may also inspect `BackseatDriver/Data/instances_data.json` for surrounding context. Use case-insensitive matching when looking up names.

## Validation Scope

Your job is to catch content and schema-adjacent issues such as:

- invented mechanics not grounded in source
- omitted major encounters or phases present in the source
- hints that are too long, vague, or not useful in-game
- role-specific hints that are unsupported or generic filler
- wrong choice of hint mode:
  - `st` should be used, but direct hints were used
  - direct hints should be used, but `st` was used
- mixed hint modes in one map
- missing names or suspicious map breakdowns

Do not duplicate the deterministic work of `instances_data_validator.py`. Focus on semantic and authoring-quality review.

## Validation Rules

Check the candidate against these rules:

1. Every boss, stage, or phase named in the output must be supported by the source.
2. No hint should introduce mechanics, mechanic names, or strategic claims absent from the source.
3. Hints should be concise and actionable enough for in-game use.
4. Role-specific hints should exist only when the source justifies them.
5. Each map must use exactly one hint mode: `st` or direct `g`/`d`/`h`/`t`.
6. If `st` is used, stage names should correspond to meaningful encounter boundaries.
7. If returning a territory entry, map IDs should be explicit and string-keyed.

## Review Output

When validating:

1. List concrete findings first.
2. For each finding, explain what is wrong and why it matters.
3. Separate true issues from uncertainty.
4. If there are no findings, state that clearly.
5. After semantic review, recommend running:
   - `python instances_compressor/instances_data_validator.py`
   - `python instances_compressor/instances_upsert.py --dry-run ...`

## Severity Guidance

Use this rough severity model:

- High: invented mechanics, wrong IDs, mixed hint modes, missing major encounters
- Medium: weak or misleading role-specific hints, suspicious stage grouping, important omissions
- Low: wording clarity, minor compression issues, optional refinement

## Response Pattern

Prefer this structure:

1. `Findings`
2. `Open Questions` if needed
3. `Verdict`

If there are no findings, say the candidate looks semantically sound and is ready for deterministic validation/tooling.
