# MonstersMap

A Dalamud plugin for Final Fantasy XIV that helps you locate and track monsters across Eorzea.
[FFXIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).

To install it, you can follow this: [my repo](https://github.com/FeriusDMS/MyDalamudPlugins).

## Features

- **Monster Search**: Search for any monster by name in your current area
- **Map Flagging**: Place flags on the map showing monster locations
- **Distance Calculation**: See how far each monster is from your position
- **Mini-map Integration**: Monster locations appear on your mini-map
- **100% Client-Side**: All operations are performed locally - no server communication

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

## Technical Details

- Built with **Dalamud API 15**
- Uses Dalamud's ObjectTable for monster detection
- Client-side coordinate conversion for map positioning
- ImGui interface for user interaction

## Requirements

- Dalamud (API 15+)
- .NET 10.0
- Windows 64-bit

## License

See LICENSE file for details
