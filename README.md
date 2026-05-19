# MonstersMap

A Dalamud plugin for Final Fantasy XIV that helps you locate and track monsters across Eorzea.

## Features

- **Monster Search**: Search for any monster by name in your current area
- **Map Flagging**: Place flags on the map showing monster locations
- **Distance Calculation**: See how far each monster is from your position
- **Mini-map Integration**: Monster locations appear on your mini-map
- **100% Client-Side**: All operations are performed locally - no server communication

## Installation

1. Add this plugin to your Dalamud plugin repository
2. Use `/xlplugins` to open the plugin installer
3. Search for "MonstersMap" and install
4. Reload your UI or restart FFXIV

## Usage

### Via Command
Type `/monsters` in the chat to open the monster search window

### Via xlplugins
1. Open `/xlplugins`
2. Navigate to "Installed Plugins"
3. Find "MonstersMap" and click to toggle the window

### Searching for Monsters
1. Open the MonstersMap window
2. Enter a monster name (partial names work)
3. Click "Search" or press Enter
4. Select a monster from the list
5. Click "Flag Location" to mark it on your map

## Safety

This plugin:
- ✅ Only reads client-side data (ObjectTable)
- ✅ Does NOT communicate with any servers
- ✅ Does NOT modify game files
- ✅ Does NOT interact with server state
- ✅ Fully compliant with Dalamud plugin guidelines

## Technical Details

- Built with **Dalamud API 15**
- Uses Dalamud's ObjectTable for monster detection
- Client-side coordinate conversion for map positioning
- ImGui interface for user interaction

## Requirements

- Dalamud (API 15+)
- .NET 9.0
- Windows 64-bit

## License

See LICENSE file for details

## Author

Andrea
