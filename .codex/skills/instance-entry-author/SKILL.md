---
name: instance-entry-author
description: Create BackseatDriver instance hint JSON from user-provided source material such as pasted wiki text or mechanic notes. Use this when the task is to turn one FFXIV duty or instance description into a schema-valid map entry or territory entry for BackseatDriver, grounded in the provided content and ready for validation or upsert.
---

# Instance Entry Author

Turn source material for a single piece of FFXIV content into `BackseatDriver` instances-data JSON.

This skill is for authoring. It does not scrape the source itself. The user should provide the relevant text, notes, or excerpts.

## Load These References First

Before authoring, read:

- `instances_compressor/instances_data_schema.md`
- `AGENTS.md`

Use the schema document as the contract for output shape and field semantics.

## Expected Inputs

Gather or infer these from the user request and supplied source text:

- content name
- territory ID
- territory name if known
- map ID or map IDs if known
- whether the content should be represented as:
  - one map entry, or
  - one full territory entry with one or more maps
- source material that grounds the hints

If IDs are missing, first inspect `BackseatDriver/Data/instances_data.json` because the repository is already seeded with many territory and map names.

When resolving IDs from the existing dataset:

- search territory and map names case-insensitively
- use the existing seeded names as the primary local lookup source
- only ask the user for IDs when the lookup is ambiguous or missing
- if multiple matches exist, present the ambiguity instead of guessing

If IDs still cannot be resolved, do not invent them. Ask for them or state that the output is provisional and needs IDs filled before upsert.

If the user provides an enemy ID and wants attack IDs from the BSD log, use:

- `python instances_compressor/bsd_log_extract.py --log <path-to-bsd-log> --enemy-id <enemy-id>`

This returns deduplicated `attack name - attack id` pairs for the specified enemy ID and can be used as source material for coach hints in `c`.

## Naming Defaults

Use these defaults unless the user asks otherwise:

- for map-level `en`, use the territory or duty name
- for stage-level `en` inside `st`, use boss or encounter names

If a map name would require guessing a boss-to-map assignment, stop and ask instead of inferring it.

## Output Modes

Choose exactly one of these:

### 1. Map Entry

Use when the user wants content for a single map ID.

Return one JSON object shaped like a map entry.

### 2. Territory Entry

Use when the content naturally maps to a full territory payload, especially if:

- the user thinks in terms of one content item rather than one map object
- the content spans multiple maps
- the caller intends to use `instances_upsert.py --territory-entry-file`

Return one JSON object shaped like a territory entry containing `en` and `maps`.

## Authoring Rules

Follow these rules strictly:

1. Ground every hint in the provided source text. Do not invent mechanics.
2. Prefer short, actionable hints suitable for in-game reading.
3. Use either `st` or direct map-level `g`/`d`/`h`/`t` hints for a map, never both.
4. Use `st` when the duty is better modeled as separate bosses, encounters, or phases.
5. Use direct map-level hints only when one shared set of hints is enough.
6. Keep names in `en`.
7. Prefer `""` for intentionally empty hint fields in newly authored content.
8. Only add coach hints in `c` when the source explicitly includes enemy/action/debuff IDs or the user provided them.
9. If the source is ambiguous, note the ambiguity instead of guessing.
10. If a dungeon has multiple seeded map IDs but the source does not clearly establish which boss belongs to which map, stop and ask before assigning bosses to maps.

Boss-to-map ambiguity is a hard stop, not an invitation to reason longer.

## Compression Rules

Compress source text into hints carefully:

- Preserve mechanic meaning, not wording.
- Remove trivia, lore, and flavor text.
- Keep role-specific hints only when the source justifies them.
- Do not force all four role fields to be populated.
- Prefer newline-separated quick bullets inside each populated hint field, using `\n` between bullets.
- Prefer 1-3 short bullets per field, not a full fight guide.
- Prioritize mechanics a player can react to quickly in duty: stacks, spreads, gaze, knockback, adds, tankbusters, raidwides, arena changes.
- Omit low-value detail such as story framing, exact ability sequencing, or mechanics that are too minor to matter in a quick-reference plugin.
- Only populate `h` or `t` when the source supports a genuinely role-specific action or risk.
- Keep each bullet short enough to scan instantly in the plugin UI.
- When a field needs multiple points, format them like `"- First point.\n- Second point."` rather than a dense paragraph.

Good:

- `"-Stack for shared damage.\n-Spread for the follow-up circles."`
- `"-Tankbuster with cleave.\n-Face boss away from party."`
- `"-Prepare healing after repeated raidwides.\n-Cleanse poison if possible."`

Bad:

- long narrative retellings
- speculative optimizations not grounded in source
- role tips that are just generic MMO advice
- copying the wiki almost verbatim
- one long paragraph when the same content can be split into fast-scanning bullets

## Hand-Off To Tooling

After generating JSON, prefer the local tools instead of manual edits:

- Validate whole-file structure with:
  - `python instances_compressor/instances_data_validator.py`
- Upsert one map entry with:
  - `python instances_compressor/instances_upsert.py --territory-id <tid> --map-id <mid> --entry-file <file>`
- Upsert one territory entry with:
  - `python instances_compressor/instances_upsert.py --territory-id <tid> --territory-entry-file <file>`

Use `--dry-run` first when applying authored content unless the user explicitly wants the file written immediately.

## Response Pattern

When asked to author content:

1. State whether you are returning a map entry or territory entry.
2. Mention any missing IDs or ambiguity in one short paragraph if needed.
3. Return only the JSON payload in a fenced `json` block.

If the user also wants confidence checks before applying the entry, hand the candidate output to `instance-entry-validator` before running the local upsert tooling.
