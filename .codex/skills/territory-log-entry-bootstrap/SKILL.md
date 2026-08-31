---
name: territory-log-entry-bootstrap
description: Extract observed maps, enemies, and enemy actions for one BackseatDriver territory ID from bsd-log.log. Use when bootstrapping or checking an instance entry from local coach/map logs; do not use as a substitute for authoring mechanic hints from source material.
---

# Territory Log Entry Bootstrap

Turn a territory ID and a BackseatDriver session log into deterministic ID evidence for entry authoring.

## Run the extractor

Use the bundled script:

```powershell
python .codex/skills/territory-log-entry-bootstrap/scripts/extract_territory.py --log <bsd-log.log> --territory-id <id>
```

Use `--format text` for a compact review. JSON is the default and should be retained when another tool or skill will consume the result. The usual Windows log path is `%APPDATA%\XIVLauncher\pluginConfigs\BackseatDriver\bsd-log.log`; reading it may require user approval because it is outside the repository.

## Interpret the result

The parser follows explicit map transitions. A target-territory visit begins on a transition to the requested territory and ends on a transition from it to another territory. The first map reported by a territory-change event is treated as stale; a map becomes current only when a subsequent transition has the target territory on both sides. Casts are attributed to the current map until another target-territory map transition occurs.

Use:

- `maps` for observed map IDs and names
- each map's `enemies` for enemy data IDs/names and observed action IDs/names
- `visits` and line spans as provenance, especially when the log contains repeated runs
- `unassigned_casts` as a warning that casts appeared before a reliable target map was established

Repeated observations are deduplicated by numeric ID. Preserve differing names in `observed_names`; they may expose localization, renamed actions, or generic `attack` records.

## Continue the entry workflow

This output resolves IDs; it does not establish mechanic meaning or hint wording. When the user wants a complete entry, pass the extracted evidence along with their wiki text or encounter notes into `instance-entry-orchestrator`. Do not invent hints from cast names alone.

Before modifying data, still follow the repository schema and validation/upsert workflow. Only write `BackseatDriver/Data/instances_data.json` when the user asked to apply the entry.

## Report gaps

State clearly when no visit was found, no reliable maps were established, or casts were unassigned. A map or enemy can be incomplete if it was not observed during combat; absence from the log is not proof that it does not exist.
