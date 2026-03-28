---
name: instance-entry-workflow
description: Orchestrate the BackseatDriver entry-authoring workflow from pasted source text. Use this when the user provides a wiki entry or encounter notes and wants the full flow: resolve IDs from existing data when possible, draft candidate JSON, validate it semantically, and prepare or run a dry-run upsert.
---

# Instance Entry Workflow

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
3. Decide whether the output should be:
   - one map entry, or
   - one territory entry
4. Hand the source text and resolved context to `instance-entry-author`.
5. Hand the candidate JSON and original source text to `instance-entry-validator`.
6. If the semantic review passes, prepare local tooling:
   - `instances_data_validator.py`
   - `instances_upsert.py --dry-run`
7. Only write for real if the user asked to apply the change or clearly wants execution beyond review.

## ID Resolution Rules

Use `BackseatDriver/Data/instances_data.json` as the first lookup source.

When searching:

- match territory and map names case-insensitively
- prefer exact or near-exact seeded matches
- if multiple plausible matches exist, stop and present the ambiguity
- do not invent IDs

If the dataset does not contain enough information to resolve IDs, ask the user for the missing ID values or return provisional JSON without applying it.

## Decision Rules

Choose map-entry mode when:

- one map ID clearly represents the content
- the user wants a focused update

Choose territory-entry mode when:

- the content naturally spans multiple maps
- the content is represented in the repo as one territory payload
- the user is thinking in terms of one duty/content item rather than one map

## Execution Rules

After authoring and semantic review:

- if the candidate has findings, do not upsert yet
- if there are no findings, prefer writing the candidate to a temp JSON file and running `instances_upsert.py --dry-run`
- only run a real upsert when the user asked to modify the dataset now

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
