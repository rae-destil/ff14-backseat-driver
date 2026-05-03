---
name: instance-entry-orchestrator
description: Orchestrate the BackseatDriver entry-authoring workflow from pasted source text. Use this when the user provides a wiki entry or encounter notes and wants the full flow: resolve IDs from existing data when possible, draft candidate JSON, validate it semantically, and prepare or run a dry-run upsert.
---

# Instance Entry Orchestrator

Run the current end-to-end workflow for one piece of FFXIV content using pasted source material.

This skill is the coordinator. It should route work through:

- `instance-entry-author`
- `instance-entry-validator`
- local tooling in `instances_compressor`

This version assumes the user already supplied the source text. Scraping comes later.

## Load These References First

Before orchestrating, read:

- `AGENTS.md`
- `instances_compressor/instances_data_schema.md`

Then load:

- `.codex/skills/instance-entry-author/SKILL.md`
- `.codex/skills/instance-entry-validator/SKILL.md`

## Workflow

Follow this order:

1. Identify the content item from the pasted text.
2. Resolve territory and map IDs from `BackseatDriver/Data/instances_data.json` when possible.
3. Resolve how bosses or stages map onto the available map IDs.
4. Decide whether the output should be:
   - one map entry, or
   - one territory entry
5. Hand the source text and resolved context to `instance-entry-author`.
6. Hand the candidate JSON and original source text to `instance-entry-validator`.
7. If the semantic review passes, prepare local tooling:
   - `instances_data_validator.py`
   - `instances_upsert.py --dry-run`
8. Only write for real if the user asked to apply the change or clearly wants execution beyond review.

## ID Resolution Rules

Use `BackseatDriver/Data/instances_data.json` as the first lookup source.

When searching:

- match territory and map names case-insensitively
- prefer exact or near-exact seeded matches
- if multiple plausible matches exist, stop and present the ambiguity
- do not invent IDs

If the dataset does not contain enough information to resolve IDs, ask the user for the missing ID values or return provisional JSON without applying it.

## Map-To-Boss Mapping Rules

Use this fallback order:

1. Existing explicit structure in `BackseatDriver/Data/instances_data.json`
2. Existing patterns for similar duties elsewhere in the repo
3. Clear source-text evidence that a duty should be one map with `st`

If the content has multiple seeded map IDs but the repo does not clearly show which boss belongs to which map, stop immediately and ask the user how to format the output. Do not infer a boss-to-map mapping from encounter order alone.

## Decision Rules

Choose map-entry mode when:

- one map ID clearly represents the content
- the user wants a focused update

Choose territory-entry mode when:

- the content naturally spans multiple maps
- the content is represented in the repo as one territory payload
- the user is thinking in terms of one duty/content item rather than one map

For linear dungeons:

- if one map ID clearly represents the whole duty, prefer one map entry with `st`
- if multiple map IDs clearly map to separate encounters in existing repo structure, prefer one territory entry with one map per encounter
- if that distinction is not immediately clear, stop and ask how the output should be formatted

## Stop And Ask

Stop immediately and ask the user when any of these are true:

- territory ID is ambiguous
- map ID is ambiguous
- boss-to-map mapping is unclear
- it is unclear whether the duty should be one map with `st` or one territory with multiple maps
- a map name would require guessing rather than using an existing seeded name or an explicit user instruction

Boss-to-map ambiguity is a hard stop. Do not continue authoring, validating, or preparing upsert commands until the user answers.

## Required Stop Response Format

When a hard-stop condition is hit, respond in this format and then wait:

1. `Stop Reason`: one sentence naming the ambiguity.
2. `What Is Known`: short bullet list of the IDs or names that are already confirmed.
3. `What Needs Clarification`: one sentence stating the exact formatting or mapping choice that must be provided.

Example:

1. `Stop Reason`: The repo contains multiple map IDs for this duty, but it does not establish which boss belongs to which map.
2. `What Is Known`:
   - Territory `1193` exists for `Worqor Zormor`
   - Map IDs `902`, `903`, and `904` exist
   - The pasted source describes three bosses
3. `What Needs Clarification`: Please tell me whether to model this as one map with `st`, or as one territory entry with one boss assigned to each map ID, and if the latter which boss maps to each ID.

## Infer And Label

You may infer and proceed, while labeling the assumption briefly, only when:

- territory and map IDs are unambiguous from existing repo data
- the chosen output mode is clearly established by existing repo structure
- the source text clearly supports the encounter breakdown

Do not infer across structural ambiguity. Structural ambiguity is a stop condition, not a labeling condition.

## Execution Rules

After authoring and semantic review:

- if the candidate has findings, do not upsert yet
- if there are no findings, prefer writing the candidate to a temp JSON file and running `instances_upsert.py --dry-run`
- only run a real upsert when the user asked to modify the dataset now
- do not run `instances_compressor` or regenerate `BackseatDriver/Data/instances_data.json.gz` automatically; leave that step to the user unless they explicitly ask for it

When using tooling:

- map mode:
  - `python instances_compressor/instances_upsert.py --territory-id <tid> --map-id <mid> --entry-file <file> --dry-run`
- territory mode:
  - `python instances_compressor/instances_upsert.py --territory-id <tid> --territory-entry-file <file> --dry-run`

## Response Pattern

If the user asked for generation only:

1. Briefly state the resolved content target and output mode.
2. Present the candidate JSON.
3. Present semantic review findings or state that none were found.
4. State what dry-run command should be used next.

If the user asked to apply the change:

1. Resolve IDs.
2. Generate candidate JSON.
3. Validate semantically.
4. Run dry-run tooling.
5. If dry-run passes, apply for real.
6. Report the outcome concisely.

## Non-Goals

This skill does not:

- scrape the wiki yet
- replace the author or validator skills
- bypass deterministic validation
- guess missing IDs when lookup is ambiguous
- run `instances_compressor` unless the user explicitly asks for it
