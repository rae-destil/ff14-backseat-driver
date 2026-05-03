# AGENTS.md

## Project Overview

This repository contains `BackseatDriver`, a Dalamud plugin for Final Fantasy XIV.
Its purpose is to show contextual in-game hints when the player enters an instanced duty.

The codebase is split between the plugin itself and a few utilities that help generate and maintain the instance hint data consumed by the plugin.

## Main Projects

### `BackseatDriver/`

The main plugin project.

- Framework: Dalamud plugin for FFXIV
- Output: plugin assembly plus embedded instance hint data
- Important embedded asset: `BackseatDriver/Data/instances_data.json.gz`
- Build detail: the project embeds `Data\instances_data.json.gz` as an `EmbeddedResource`

This is the runtime target. If data changes are made for duty hints, the plugin ultimately consumes the compressed JSON archive from this project.

### `instances_compressor/`

Utility for maintaining the archive consumed by the plugin.

- Reads: `BackseatDriver/Data/instances_data.json`
- Writes: `BackseatDriver/Data/instances_data.json.gz`
- Purpose: compress the canonical instance data file into the embedded plugin asset

When updating hint data, this project is part of the finalization path.

### `map_enumerator/`

Python utility that produces an initial JSON dump of territories and map IDs.

- Purpose: bootstrap or refresh the raw duty/map structure
- Role in workflow: starting point for building or extending the instance dataset

Use this when the task is about discovering new territories/maps or regenerating the structural base of the data file.

### `game_mapper/`

Playground utility for dumping Lumina rows and exploring internal game data sheets.

- Purpose: inspect game data, IDs, and related rows
- Role in workflow: investigation and reverse-engineering aid

Use this when the needed duty metadata is easier to derive from Lumina than from existing JSON.

## Data Flow

The intended data flow is:

1. Discover or refresh map and territory data with `map_enumerator` when needed.
2. Investigate game sheet details with `game_mapper` when IDs or internal names need clarification.
3. Maintain the canonical hint dataset at `BackseatDriver/Data/instances_data.json`.
4. Run `instances_compressor` to regenerate `BackseatDriver/Data/instances_data.json.gz`.
5. Build `BackseatDriver` so the compressed JSON is embedded into the plugin.

## Navigation Notes

- Start with `README.md` for the broad repo description.
- Treat `BackseatDriver/Data/instances_data.json` as the source of truth for hint content.
- Treat `BackseatDriver/Data/instances_data.json.gz` as a generated artifact used by the plugin.
- If a change touches in-game hint content, verify whether both the plain JSON and compressed archive need to be updated.

## Working Assumptions For Future Agents

- This repo is centered on duty/instance hint data for FFXIV.
- The plugin runtime depends on compressed embedded JSON, not only the plain-text source file.
- Utility projects exist to support data discovery and transformation; they are not the runtime product.
- Prefer understanding the data pipeline before editing plugin UI or runtime logic.
