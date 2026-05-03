# Instances Data Schema

This document describes the JSON structure used by `BackseatDriver/Data/instances_data.json`.

The plugin loads the compressed version of this file from `BackseatDriver/Data/instances_data.json.gz`, but the plain JSON file is the editable source of truth.

## Purpose

The file maps FFXIV territory IDs to map IDs, then stores optional role-based hint text for each map or for specific stages within that map.

At runtime, the plugin deserializes the file into:

- `Dictionary<string, TerritoryRoleHints>`
- `TerritoryRoleHints`
- `MapRoleHints`
- `RoleHints`
- `CoachHints`
- `EnemyHints`

## Top-Level Shape

The root object is a JSON object keyed by territory ID as a string.

Example:

```json
{
  "1234": {
    "en": "Example Territory",
    "maps": {
      "5678": {
        "en": "Example Map",
        "g": "General advice.",
        "d": "DPS advice.",
        "h": "Healer advice.",
        "t": "Tank advice."
      }
    }
  }
}
```

## Territory Object

Each top-level territory value has this shape:

```json
{
  "en": "Territory display name",
  "maps": {
    "<mapId>": {
      "...": "..."
    }
  }
}
```

Fields:

- `en`: English territory name.
- `maps`: Object keyed by map ID as a string.

Notes:

- Existing data may contain `null` for `en` in incomplete entries.
- Prefer writing a real name when it is known.
- `maps` should always exist, even if empty.

## Map Object

Each map inside `maps` has this shape:

```json
{
  "en": "Map display name",
  "st": [
    {
      "en": "Stage or encounter name",
      "g": "General advice",
      "d": "DPS advice",
      "h": "Healer advice",
      "t": "Tank advice"
    }
  ],
  "g": "General advice for the whole map",
  "d": "DPS advice for the whole map",
  "h": "Healer advice for the whole map",
  "t": "Tank advice for the whole map",
  "c": {
    "<enemyId>": {
      "a": {
        "<actionId>": {
          "g": "General action hint",
          "d": "DPS action hint",
          "h": "Healer action hint",
          "t": "Tank action hint"
        }
      },
      "d": {
        "<statusId>": {
          "g": "General debuff hint",
          "d": "DPS debuff hint",
          "h": "Healer debuff hint",
          "t": "Tank debuff hint"
        }
      }
    }
  }
}
```

Fields:

- `en`: English map or duty name.
- `st`: Optional array of stage-specific hint objects.
- `g`: General hint for the whole map.
- `d`: DPS-specific hint for the whole map.
- `h`: Healer-specific hint for the whole map.
- `t`: Tank-specific hint for the whole map.
- `c`: Optional coach-mode hints keyed by enemy ID.

Notes:

- The plugin treats `st` as optional.
- Each map entry should use exactly one content mode:
  - stage mode via `st`, or
  - direct map-level hints via `g`, `d`, `h`, and `t`
- Do not author a map entry with both a populated `st` array and populated direct map-level hints.
- If `st` is missing or empty, the plugin falls back to the map-level `g`, `d`, `h`, and `t` fields.
- If all map-level hint fields are empty or placeholder values and `st` is empty, the map effectively has no usable hint content.

## Stage Object

Each stage entry in `st` has this shape:

```json
{
  "en": "Boss or stage name",
  "g": "General advice",
  "d": "DPS advice",
  "h": "Healer advice",
  "t": "Tank advice"
}
```

Fields:

- `en`: Stage, boss, or encounter name shown in the UI tab.
- `g`: General advice for all players.
- `d`: DPS-specific advice.
- `h`: Healer-specific advice.
- `t`: Tank-specific advice.

Notes:

- Use `st` when a duty is better represented as several encounters, bosses, or phases.
- Use map-level hints only when one shared set of hints is enough for the whole duty.
- Do not mix stage entries with populated map-level role hints for the same map.

## Coach Hints Object

The `c` field is used by coach mode and is keyed by enemy ID as a string.

Each enemy entry contains:

- `a`: Action hints keyed by action ID as a string.
- `d`: Debuff hints keyed by status or debuff ID as a string.

Example:

```json
{
  "c": {
    "17854": {
      "a": {
        "12345": {
          "g": "Move away from the line attack.",
          "d": "",
          "h": "",
          "t": "Face the boss away from the party."
        }
      },
      "d": {
        "6789": {
          "g": "Cleanse if possible.",
          "d": "",
          "h": "Watch party health while this is active.",
          "t": ""
        }
      }
    }
  }
}
```

## Hint Text Semantics

The runtime treats these values as effectively empty:

- `""`
- `"..."`
- whitespace-only strings

Practical guidance:

- Prefer `""` for intentionally empty fields in newly written content.
- Avoid adding placeholder `"..."` in new curated entries unless you are preserving existing style for consistency.
- Do not invent hints just to fill every role field.

## Authoring Rules

When creating or editing entries:

1. Use territory IDs and map IDs as JSON object keys, stored as strings.
2. Keep names in `en`.
3. Ground every hint in source material; do not invent mechanics.
4. Keep hints short and actionable because they are surfaced in-game.
5. Put broad duty-wide guidance at map level.
6. Put boss-specific or phase-specific guidance in `st`.
7. Choose one hint mode per map: either `st` or direct map-level hints, never both.
8. Only add `c` when enemy/action/debuff IDs are known and coach mode support is intended.
9. Preserve valid JSON formatting. This file is loaded at runtime and malformed JSON breaks the plugin data load.

## Minimal Valid Shapes

### Empty Territory

```json
{
  "9999": {
    "en": "Unknown Duty",
    "maps": {}
  }
}
```

### Map-Level Hints Only

```json
{
  "9999": {
    "en": "Example Territory",
    "maps": {
      "1111": {
        "en": "Example Duty",
        "g": "Watch for roomwide AoEs.",
        "d": "Save burst for add phases.",
        "h": "Prepare for repeated raidwide damage.",
        "t": "Mitigate tankbusters and keep enemies centered."
      }
    }
  }
}
```

### Stage-Based Hints

```json
{
  "9999": {
    "en": "Example Territory",
    "maps": {
      "1111": {
        "en": "Example Duty",
        "st": [
          {
            "en": "Boss One",
            "g": "Dodge the rotating cleaves.",
            "d": "Stay on target during movement.",
            "h": "Top the party after raidwides.",
            "t": "Turn the cleave away from the party."
          },
          {
            "en": "Boss Two",
            "g": "Spread for marked explosions.",
            "d": "",
            "h": "Prepare shields before the stack marker.",
            "t": "Pick the boss back up quickly after jumps."
          }
        ]
      }
    }
  }
}
```

## Validation Checklist

Before compressing and shipping updated data:

1. Confirm the JSON parses successfully.
2. Confirm the edited territory ID and map ID are correct.
3. Confirm `en` names match the intended duty or encounter.
4. Confirm role fields are concise and source-grounded.
5. Confirm each map uses exactly one hint mode: `st` or direct map-level hints.
6. Confirm `st` is used only when encounter-level breakdown is needed.
7. Regenerate `BackseatDriver/Data/instances_data.json.gz` after changes.
