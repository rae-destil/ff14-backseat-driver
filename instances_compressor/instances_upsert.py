#!/usr/bin/env python3
"""
Safely upsert either a single map entry or a full territory entry into
BackseatDriver instances data JSON.

Single-map mode input must be one map object matching the schema documented in
instances_data_schema.md, for example:

{
  "en": "Example Duty",
  "st": [
    {
      "en": "Boss One",
      "g": "General hint",
      "d": "",
      "h": "",
      "t": ""
    }
  ]
}

Territory mode input must be one territory object, for example:

{
  "en": "Example Territory",
  "maps": {
    "5678": {
      "en": "Example Duty",
      "g": "General advice",
      "d": "",
      "h": "",
      "t": ""
    }
  }
}
"""

from __future__ import annotations

import argparse
import json
import sys
from copy import deepcopy
from pathlib import Path
from typing import Any

from instances_data_validator import default_json_path, load_json, print_report, validate_root


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Upsert a map entry into instances_data.json.")
    parser.add_argument(
        "--entry-file",
        help="Path to a JSON file containing one map entry object. Use '-' to read from stdin.",
    )
    parser.add_argument(
        "--territory-entry-file",
        help="Path to a JSON file containing one territory entry object. Use '-' to read from stdin.",
    )
    parser.add_argument("--territory-id", required=True, help="Territory ID to update.")
    parser.add_argument("--map-id", help="Map ID to update in single-map mode.")
    parser.add_argument(
        "--json-path",
        default=str(default_json_path()),
        help="Path to instances_data.json. Defaults to BackseatDriver/Data/instances_data.json.",
    )
    parser.add_argument(
        "--territory-name",
        help="Optional territory display name to write into the target territory entry.",
    )
    parser.add_argument(
        "--map-name",
        help="Optional map display name to force into the entry's 'en' field.",
    )
    parser.add_argument(
        "--create-missing-territory",
        action="store_true",
        help="Create the territory entry when it does not already exist.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate and print the result without writing the file.",
    )
    parser.add_argument(
        "--strict-content",
        action="store_true",
        help="Run validator content warnings on the resulting dataset before writing.",
    )
    parser.add_argument(
        "--include-enumerated-unfilled",
        action="store_true",
        help="Include enumerated null-name warnings in the final validation report.",
    )
    return parser.parse_args()


def read_entry(path_arg: str) -> Any:
    if path_arg == "-":
        return json.load(sys.stdin)

    path = Path(path_arg).resolve()
    return load_json(path)


def ensure_map_entry_shape(entry: Any) -> dict[str, Any]:
    if not isinstance(entry, dict):
        raise ValueError(f"entry must be a JSON object, got {type(entry).__name__}")
    return deepcopy(entry)


def ensure_territory_entry_shape(entry: Any) -> dict[str, Any]:
    if not isinstance(entry, dict):
        raise ValueError(f"territory entry must be a JSON object, got {type(entry).__name__}")
    return deepcopy(entry)


def write_json(path: Path, data: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, indent=4, ensure_ascii=False)
        handle.write("\n")


def main() -> int:
    args = parse_args()
    json_path = Path(args.json_path).resolve()

    if bool(args.entry_file) == bool(args.territory_entry_file):
        print(
            "Pass exactly one of --entry-file or --territory-entry-file.",
            file=sys.stderr,
        )
        return 2

    if not args.territory_id.isdigit():
        print("--territory-id must be numeric.", file=sys.stderr)
        return 2

    is_map_mode = args.entry_file is not None
    if is_map_mode:
        if not args.map_id:
            print("--map-id is required when using --entry-file.", file=sys.stderr)
            return 2
        if not args.map_id.isdigit():
            print("--map-id must be numeric.", file=sys.stderr)
            return 2
    elif args.map_id:
        print("--map-id is only valid with --entry-file.", file=sys.stderr)
        return 2

    if not json_path.exists():
        print(f"Instances data file not found: {json_path}", file=sys.stderr)
        return 2

    try:
        data = load_json(json_path)
        if is_map_mode:
            entry = ensure_map_entry_shape(read_entry(args.entry_file))
        else:
            territory_entry = ensure_territory_entry_shape(read_entry(args.territory_entry_file))
    except json.JSONDecodeError as exc:
        print(f"Invalid JSON: {exc}", file=sys.stderr)
        return 2
    except OSError as exc:
        print(f"Failed to read input: {exc}", file=sys.stderr)
        return 2
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 2

    if not isinstance(data, dict):
        print("instances_data.json root must be a JSON object.", file=sys.stderr)
        return 2

    territory_id = args.territory_id
    if territory_id not in data:
        if not args.create_missing_territory:
            print(
                f"Territory '{territory_id}' does not exist. "
                "Pass --create-missing-territory to create it.",
                file=sys.stderr,
            )
            return 2
        data[territory_id] = {
            "en": args.territory_name or "",
            "maps": {},
        }

    territory = data[territory_id]
    if not isinstance(territory, dict):
        print(f"Territory '{territory_id}' is not an object.", file=sys.stderr)
        return 2

    maps = territory.get("maps")
    if maps is None:
        maps = {}
        territory["maps"] = maps
    if not isinstance(maps, dict):
        print(f"Territory '{territory_id}' has a non-object 'maps' field.", file=sys.stderr)
        return 2

    if is_map_mode:
        map_id = args.map_id

        if args.territory_name:
            territory["en"] = args.territory_name
        elif "en" not in territory:
            territory["en"] = ""

        if args.map_name:
            entry["en"] = args.map_name

        maps[map_id] = entry
    else:
        if args.map_name:
            print("--map-name is only valid with --entry-file.", file=sys.stderr)
            return 2

        incoming_maps = territory_entry.get("maps")
        if incoming_maps is None:
            print("territory entry must include a 'maps' object.", file=sys.stderr)
            return 2
        if not isinstance(incoming_maps, dict):
            print("territory entry field 'maps' must be an object.", file=sys.stderr)
            return 2

        if args.territory_name:
            territory["en"] = args.territory_name
        elif "en" in territory_entry:
            territory["en"] = territory_entry["en"]
        elif "en" not in territory:
            territory["en"] = ""

        territory["maps"] = incoming_maps

    reporter = validate_root(
        data,
        strict_content=args.strict_content,
        include_enumerated_unfilled=args.include_enumerated_unfilled,
    )
    print_report(json_path, reporter)

    if reporter.errors:
        print("\nAborting write because validation failed.", file=sys.stderr)
        return 1

    if args.dry_run:
        print("\nDry run only; no file was written.")
        return 0

    try:
        write_json(json_path, data)
    except OSError as exc:
        print(f"Failed to write {json_path}: {exc}", file=sys.stderr)
        return 2

    if is_map_mode:
        print(f"\nUpserted territory {territory_id}, map {map_id} into {json_path}.")
    else:
        print(f"\nUpserted territory {territory_id} into {json_path}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
