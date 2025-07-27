# BSDriverPlugin

BSDriverPlugin is a [Dalamud](https://dalamud.dev/) plugin for FINAL FANTASY XIV that provides contextual role-based advice and hints for dungeons, trials, and other content. 
It is designed to help players quickly access helpful tips for their current job and instance, directly in-game. The main audience is players like me that forget mechanics or need a quick refresher on their role's responsibilities in a specific content.

---

## Features

- **Contextual Hints:** Displays general and role-specific advice for your current map or stage.
- **Role Awareness:** Supports Tank, Healer, and DPS roles, with tailored hints for each.
- **Stage/Phase Support:** Hints can be organized by stage or phase within a duty.
- **UI Integration:** Provides a main window and configuration window for user interaction.
- **Easy Access:** All features are accessible via simple chat commands.

---

## Registered Commands

| Command              | Description                                         |
|----------------------|-----------------------------------------------------|
| `/pbsdriver`         | Opens the main Backseat Driver UI.                  |
| `/pbsdriver-quick`   | Prints hints for your job in the current instance.  |

---

## Solution Structure

The solution is organized as follows:

- **BSDriverPlugin/**
  - Contains the main plugin code, including commands, UI, and configuration.
- **instances_compressor/**
  - Utility that compresses instances_data.json with gzip._
- **map_enumerator/**
  - Python utility to enumerate all maps and territories from FFXIVAPI into the instances_data.json schema._
- **plugin_tester/**
  - Dummy tester for testing basic stuff like successful inventory extraction from the assembly.

---

## BSDriverPlugin Structure

- **Plugin.cs**
  - Registers commands and hooks into Dalamud services.
  - Loads and parses the embedded instance data.
  - Manages the main and configuration windows.
  - Handles user command callbacks and UI events.

- **Configuration.cs**
  - Stores and manages user settings for the plugin.

- **DriverWindow.cs**
  - Renders the main UI for browsing and sending hints.
  - Organizes hints by map and stage, with role-specific buttons.

- **ConfigWindow.cs**
  - Provides a UI for adjusting plugin settings.

- **instances_data.json**
  - Contains all hint data, organized by territory, map, and stage, with fields for general, tank, healer, and DPS advice.

---

## Building & Usage

1. Open the solution in Visual Studio 2022 or later.
2. Build the solution (requires .NET 9).
3. Add the built DLL to Dalamud's Dev Plugin Locations.
4. Use `/pbsdriver` or `/pbsdriver-quick` in-game to access plugin features.
5. ???
6. Profit!
