#!/usr/bin/env python3
"""Extract map-scoped enemy/action observations for a territory from a BSD log."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

MAP_RE = re.compile(
    r"\[MAP\].*?from territory=(?P<from_tid>unknown|\d+)"
    r"(?: \((?P<from_tname>.*?)\))?, map=(?P<from_mid>unknown|\d+)"
    r"(?: \((?P<from_mname>.*?)\))?, to territory=(?P<to_tid>\d+)"
    r" \((?P<to_tname>.*?)\), map=(?P<to_mid>\d+) \((?P<to_mname>.*?)\)"
)
CAST_RE = re.compile(
    r"\[COACH\]\s+(?P<enemy_name>.+?) \((?P<enemy_id>\d+)\) casts "
    r"(?P<action_name>.+?) \((?P<action_id>\d+)\)\."
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Extract observed maps, enemies, and actions for one territory ID.")
    parser.add_argument("--log", required=True, help="Path to bsd-log.log")
    parser.add_argument("--territory-id", required=True, help="Numeric territory ID")
    parser.add_argument("--format", choices=("json", "text"), default="json")
    return parser.parse_args()


def add_name(names: list[str], value: str) -> None:
    value = value.strip()
    if value and value not in names:
        names.append(value)


def extract(lines: list[str], territory_id: str) -> dict[str, Any]:
    maps: dict[str, dict[str, Any]] = {}
    visits: list[dict[str, Any]] = []
    active_visit: dict[str, Any] | None = None
    current_map: str | None = None
    territory_names: list[str] = []
    unassigned_casts = 0

    for line_number, line in enumerate(lines, 1):
        map_match = MAP_RE.search(line)
        if map_match:
            from_tid = map_match.group("from_tid")
            to_tid = map_match.group("to_tid")
            if active_visit is not None and from_tid == territory_id and to_tid != territory_id:
                active_visit["end_line"] = line_number
                active_visit = None
                current_map = None
            if to_tid == territory_id:
                add_name(territory_names, map_match.group("to_tname"))
                if active_visit is None:
                    active_visit = {"start_line": line_number, "end_line": None, "maps": [], "cast_count": 0}
                    visits.append(active_visit)
                # Cross-territory events commonly carry the prior territory's map.
                # Trust a map only when both transition sides name the target territory.
                if from_tid == territory_id:
                    current_map = map_match.group("to_mid")
                    map_entry = maps.setdefault(current_map, {"observed_names": [], "enemies": {}})
                    add_name(map_entry["observed_names"], map_match.group("to_mname"))
                    if current_map not in active_visit["maps"]:
                        active_visit["maps"].append(current_map)
            continue

        cast_match = CAST_RE.search(line)
        if not cast_match or active_visit is None:
            continue
        active_visit["cast_count"] += 1
        if current_map is None:
            unassigned_casts += 1
            continue
        map_entry = maps[current_map]
        enemy_id = cast_match.group("enemy_id")
        enemy = map_entry["enemies"].setdefault(enemy_id, {"observed_names": [], "actions": {}})
        add_name(enemy["observed_names"], cast_match.group("enemy_name"))
        action_id = cast_match.group("action_id")
        action = enemy["actions"].setdefault(action_id, {"observed_names": []})
        add_name(action["observed_names"], cast_match.group("action_name"))

    if active_visit is not None:
        active_visit["end_line"] = len(lines)

    def numeric_items(values: dict[str, Any]) -> dict[str, Any]:
        return dict(sorted(values.items(), key=lambda item: int(item[0])))

    for map_entry in maps.values():
        map_entry["enemies"] = numeric_items(map_entry["enemies"])
        for enemy in map_entry["enemies"].values():
            enemy["actions"] = numeric_items(enemy["actions"])
    return {
        "territory_id": territory_id,
        "observed_names": territory_names,
        "visits": visits,
        "maps": numeric_items(maps),
        "unassigned_casts": unassigned_casts,
    }


def render_text(result: dict[str, Any]) -> str:
    output = [
        f"Territory {result['territory_id']}: {', '.join(result['observed_names']) or 'unknown'}",
        f"Visits: {len(result['visits'])}; unassigned casts: {result['unassigned_casts']}",
    ]
    for map_id, map_entry in result["maps"].items():
        output.append(f"Map {map_id}: {', '.join(map_entry['observed_names']) or 'unknown'}")
        for enemy_id, enemy in map_entry["enemies"].items():
            output.append(f"  Enemy {enemy_id}: {', '.join(enemy['observed_names'])}")
            for action_id, action in enemy["actions"].items():
                output.append(f"    {action_id}: {', '.join(action['observed_names'])}")
    return "\n".join(output)


def main() -> int:
    args = parse_args()
    if not args.territory_id.isdigit():
        print("--territory-id must be numeric.", file=sys.stderr)
        return 2
    log_path = Path(args.log).resolve()
    if not log_path.is_file():
        print(f"Log file not found: {log_path}", file=sys.stderr)
        return 2
    try:
        lines = log_path.read_text(encoding="utf-8-sig").splitlines()
    except OSError as exc:
        print(f"Failed to read {log_path}: {exc}", file=sys.stderr)
        return 2
    result = extract(lines, args.territory_id)
    print(render_text(result) if args.format == "text" else json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
