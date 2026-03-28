#!/usr/bin/env python3
"""
Validate BackseatDriver instances data JSON.

This script performs deterministic structural checks so skills do not need to
load the entire dataset into context just to verify basic correctness.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

MAP_ALLOWED_FIELDS = {"en", "st", "g", "d", "h", "t", "c"}
STAGE_ALLOWED_FIELDS = {"en", "g", "d", "h", "t"}
COACH_ALLOWED_FIELDS = {"a", "d"}
ENEMY_HINT_ALLOWED_FIELDS = {"g", "d", "h", "t"}
PLACEHOLDER_VALUES = {"..."}
DIRECT_HINT_FIELDS = ("g", "d", "h", "t")


class ValidationReporter:
    def __init__(self, *, strict_content: bool = False, include_enumerated_unfilled: bool = False) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.territory_count = 0
        self.map_count = 0
        self.stage_count = 0
        self.strict_content = strict_content
        self.include_enumerated_unfilled = include_enumerated_unfilled

    def error(self, path: str, message: str) -> None:
        self.errors.append(f"{path}: {message}")

    def warn(self, path: str, message: str) -> None:
        self.warnings.append(f"{path}: {message}")

    @property
    def ok(self) -> bool:
        return not self.errors


def is_effectively_empty(value: Any) -> bool:
    if value is None:
        return True
    if not isinstance(value, str):
        return False
    return not value.strip() or value.strip() in PLACEHOLDER_VALUES


def is_non_empty_hint(value: Any) -> bool:
    return isinstance(value, str) and not is_effectively_empty(value)


def validate_stringish_field(
    reporter: ValidationReporter,
    path: str,
    field_name: str,
    value: Any,
    *,
    allow_null: bool = False,
    warn_on_null: bool = False,
) -> None:
    if value is None:
        if not allow_null:
            reporter.error(path, f"field '{field_name}' must be a string, got null")
        elif warn_on_null and reporter.include_enumerated_unfilled:
            reporter.warn(path, f"field '{field_name}' is null")
        return

    if not isinstance(value, str):
        reporter.error(path, f"field '{field_name}' must be a string, got {type(value).__name__}")


def validate_enemy_hints(reporter: ValidationReporter, path: str, hints: Any) -> None:
    if not isinstance(hints, dict):
        reporter.error(path, f"expected object, got {type(hints).__name__}")
        return

    extra_fields = sorted(set(hints) - ENEMY_HINT_ALLOWED_FIELDS)
    if extra_fields:
        reporter.warn(path, f"unexpected fields: {', '.join(extra_fields)}")

    for field in DIRECT_HINT_FIELDS:
        if field in hints:
            validate_stringish_field(reporter, path, field, hints[field])


def validate_id_keyed_enemy_hint_map(
    reporter: ValidationReporter, path: str, data: Any, description: str
) -> None:
    if not isinstance(data, dict):
        reporter.error(path, f"{description} must be an object, got {type(data).__name__}")
        return

    for key, value in data.items():
        child_path = f"{path}.{key}"
        if not isinstance(key, str):
            reporter.error(path, f"{description} key must be a string, got {type(key).__name__}")
            continue
        if not key.isdigit():
            reporter.warn(child_path, f"{description} key is not numeric")
        validate_enemy_hints(reporter, child_path, value)


def validate_coach_hints(reporter: ValidationReporter, path: str, coach_hints: Any) -> None:
    if not isinstance(coach_hints, dict):
        reporter.error(path, f"field 'c' must be an object, got {type(coach_hints).__name__}")
        return

    for enemy_id, enemy_hints in coach_hints.items():
        enemy_path = f"{path}.{enemy_id}"
        if not isinstance(enemy_id, str):
            reporter.error(path, f"enemy id key must be a string, got {type(enemy_id).__name__}")
            continue
        if not enemy_id.isdigit():
            reporter.warn(enemy_path, "enemy id key is not numeric")
        if not isinstance(enemy_hints, dict):
            reporter.error(enemy_path, f"coach enemy entry must be an object, got {type(enemy_hints).__name__}")
            continue

        extra_fields = sorted(set(enemy_hints) - COACH_ALLOWED_FIELDS)
        if extra_fields:
            reporter.warn(enemy_path, f"unexpected fields: {', '.join(extra_fields)}")

        if "a" in enemy_hints:
            validate_id_keyed_enemy_hint_map(reporter, f"{enemy_path}.a", enemy_hints["a"], "action hint map")
        if "d" in enemy_hints:
            validate_id_keyed_enemy_hint_map(reporter, f"{enemy_path}.d", enemy_hints["d"], "debuff hint map")


def validate_stage(reporter: ValidationReporter, path: str, stage: Any) -> None:
    if not isinstance(stage, dict):
        reporter.error(path, f"stage entry must be an object, got {type(stage).__name__}")
        return

    extra_fields = sorted(set(stage) - STAGE_ALLOWED_FIELDS)
    if extra_fields:
        reporter.warn(path, f"unexpected fields: {', '.join(extra_fields)}")

    if "en" not in stage:
        reporter.error(path, "missing required field 'en'")
    else:
        validate_stringish_field(reporter, path, "en", stage["en"])

    for field in DIRECT_HINT_FIELDS:
        if field in stage:
            validate_stringish_field(reporter, path, field, stage[field])

    if not any(is_non_empty_hint(stage.get(field)) for field in DIRECT_HINT_FIELDS):
        if reporter.strict_content:
            reporter.warn(path, "stage has no usable hints")


def validate_map(reporter: ValidationReporter, path: str, map_data: Any) -> None:
    if not isinstance(map_data, dict):
        reporter.error(path, f"map entry must be an object, got {type(map_data).__name__}")
        return

    reporter.map_count += 1

    extra_fields = sorted(set(map_data) - MAP_ALLOWED_FIELDS)
    if extra_fields:
        reporter.warn(path, f"unexpected fields: {', '.join(extra_fields)}")

    if "en" in map_data:
        validate_stringish_field(reporter, path, "en", map_data["en"], allow_null=True, warn_on_null=True)
    else:
        reporter.warn(path, "missing field 'en'")

    direct_hints_present = any(is_non_empty_hint(map_data.get(field)) for field in DIRECT_HINT_FIELDS)

    stages = map_data.get("st")
    stages_present = False
    if "st" in map_data:
        if not isinstance(stages, list):
            reporter.error(path, f"field 'st' must be an array, got {type(stages).__name__}")
        else:
            stages_present = len(stages) > 0
            seen_stage_names: set[str] = set()
            for index, stage in enumerate(stages):
                stage_path = f"{path}.st[{index}]"
                validate_stage(reporter, stage_path, stage)
                reporter.stage_count += 1
                if isinstance(stage, dict):
                    stage_name = stage.get("en")
                    if isinstance(stage_name, str) and stage_name.strip():
                        normalized = stage_name.strip().casefold()
                        if normalized in seen_stage_names:
                            reporter.warn(stage_path, f"duplicate stage name '{stage_name}'")
                        else:
                            seen_stage_names.add(normalized)

    if stages_present and direct_hints_present:
        reporter.error(path, "map mixes stage hints in 'st' with direct map-level hints")

    if "c" in map_data:
        validate_coach_hints(reporter, f"{path}.c", map_data["c"])

    coach_present = isinstance(map_data.get("c"), dict) and bool(map_data["c"])
    if reporter.strict_content and not stages_present and not direct_hints_present and not coach_present:
        reporter.warn(path, "map has no usable hint content")


def validate_territory(reporter: ValidationReporter, territory_id: str, territory_data: Any) -> None:
    path = territory_id
    if not isinstance(territory_data, dict):
        reporter.error(path, f"territory entry must be an object, got {type(territory_data).__name__}")
        return

    reporter.territory_count += 1

    if "en" in territory_data:
        validate_stringish_field(reporter, path, "en", territory_data["en"], allow_null=True, warn_on_null=True)
    else:
        reporter.warn(path, "missing field 'en'")

    if "maps" not in territory_data:
        reporter.error(path, "missing required field 'maps'")
        return

    maps = territory_data["maps"]
    if not isinstance(maps, dict):
        reporter.error(path, f"field 'maps' must be an object, got {type(maps).__name__}")
        return

    for map_id, map_data in maps.items():
        map_path = f"{territory_id}.maps.{map_id}"
        if not isinstance(map_id, str):
            reporter.error(path, f"map id key must be a string, got {type(map_id).__name__}")
            continue
        if not map_id.isdigit():
            reporter.warn(map_path, "map id key is not numeric")
        validate_map(reporter, map_path, map_data)


def validate_root(
    data: Any, *, strict_content: bool = False, include_enumerated_unfilled: bool = False
) -> ValidationReporter:
    reporter = ValidationReporter(
        strict_content=strict_content,
        include_enumerated_unfilled=include_enumerated_unfilled,
    )

    if not isinstance(data, dict):
        reporter.error("$", f"root must be an object, got {type(data).__name__}")
        return reporter

    for territory_id, territory_data in data.items():
        if not isinstance(territory_id, str):
            reporter.error("$", f"territory id key must be a string, got {type(territory_id).__name__}")
            continue
        if not territory_id.isdigit():
            reporter.warn(territory_id, "territory id key is not numeric")
        validate_territory(reporter, territory_id, territory_data)

    return reporter


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def default_json_path() -> Path:
    return Path(__file__).resolve().parents[1] / "BackseatDriver" / "Data" / "instances_data.json"


def print_report(path: Path, reporter: ValidationReporter) -> None:
    print(f"Validated: {path}")
    print(
        "Summary: "
        f"{reporter.territory_count} territories, "
        f"{reporter.map_count} maps, "
        f"{reporter.stage_count} stages"
    )
    print(f"Errors: {len(reporter.errors)}")
    print(f"Warnings: {len(reporter.warnings)}")

    if reporter.errors:
        print("\nError details:")
        for message in reporter.errors:
            print(f"- {message}")

    if reporter.warnings:
        print("\nWarning details:")
        for message in reporter.warnings:
            print(f"- {message}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate BackseatDriver instances data JSON.")
    parser.add_argument(
        "json_path",
        nargs="?",
        default=str(default_json_path()),
        help="Path to the instances_data.json file to validate.",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Treat warnings as a non-zero exit condition.",
    )
    parser.add_argument(
        "--strict-content",
        action="store_true",
        help="Include warnings for structurally valid but unfilled entries, such as placeholder-only maps.",
    )
    parser.add_argument(
        "--include-enumerated-unfilled",
        action="store_true",
        help="Include warnings for null 'en' fields in enumerated-but-unfilled entries.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    json_path = Path(args.json_path).resolve()

    if not json_path.exists():
        print(f"File not found: {json_path}", file=sys.stderr)
        return 2

    try:
        data = load_json(json_path)
    except json.JSONDecodeError as exc:
        print(f"Invalid JSON in {json_path}: {exc}", file=sys.stderr)
        return 2
    except OSError as exc:
        print(f"Failed to read {json_path}: {exc}", file=sys.stderr)
        return 2

    reporter = validate_root(
        data,
        strict_content=args.strict_content,
        include_enumerated_unfilled=args.include_enumerated_unfilled,
    )
    print_report(json_path, reporter)

    if reporter.errors:
        return 1
    if args.strict and reporter.warnings:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
