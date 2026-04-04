#!/usr/bin/env python3
"""
Extract unique attack name/id pairs for a given enemy ID from a BSD log.

Expected log line shape:
    <anything> (<enemy id>) casts <attack name> (<attack id>). <anything>

Example:
    Forgiven Obscenity (18688) casts Penance Pianissimo (44237).
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

LINE_RE = re.compile(
    r"\((?P<enemy_id>\d+)\)\s+casts\s+(?P<attack_name>.+?)\s+\((?P<attack_id>\d+)\)\."
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract unique attack name/id pairs for an enemy ID from a BSD log."
    )
    parser.add_argument(
        "--log",
        required=True,
        help="Path to the BSD log file.",
    )
    parser.add_argument(
        "--enemy-id",
        required=True,
        help="Enemy ID to filter on.",
    )
    parser.add_argument(
        "--format",
        choices=("text", "json"),
        default="text",
        help="Output format. Defaults to text.",
    )
    return parser.parse_args()


def load_lines(path: Path) -> list[str]:
    with path.open("r", encoding="utf-8-sig") as handle:
        return handle.readlines()


def extract_pairs(lines: list[str], enemy_id: str) -> list[dict[str, str]]:
    dedup: dict[tuple[str, str], dict[str, str]] = {}

    for raw_line in lines:
        match = LINE_RE.search(raw_line)
        if not match:
            continue

        if match.group("enemy_id") != enemy_id:
            continue

        attack_name = match.group("attack_name").strip()
        attack_id = match.group("attack_id")
        key = (attack_name.casefold(), attack_id)

        if key not in dedup:
            dedup[key] = {
                "attack_name": attack_name,
                "attack_id": attack_id,
            }

    return sorted(dedup.values(), key=lambda item: (item["attack_name"].casefold(), int(item["attack_id"])))


def main() -> int:
    args = parse_args()
    log_path = Path(args.log).resolve()

    if not args.enemy_id.isdigit():
        print("--enemy-id must be numeric.", file=sys.stderr)
        return 2

    if not log_path.exists():
        print(f"Log file not found: {log_path}", file=sys.stderr)
        return 2

    try:
        lines = load_lines(log_path)
    except OSError as exc:
        print(f"Failed to read {log_path}: {exc}", file=sys.stderr)
        return 2

    results = extract_pairs(lines, args.enemy_id)

    if args.format == "json":
        print(json.dumps(results, indent=2, ensure_ascii=False))
    else:
        for item in results:
            print(f"{item['attack_name']} - {item['attack_id']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
