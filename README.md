# MonstersMap

A Dalamud plugin for Final Fantasy XIV that helps you locate and track monsters across Eorzea.
[FFXIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).

To install it, you can follow this: [my repo](https://github.com/FeriusDMS/MyDalamudPlugins).

## Features

- **Monster Search**: Search for any monster by name and get its exact in-world position
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
5. Click "Flag Location" to mark its exact location on the map

## Build Validation Without Installing .NET Locally

The real build and test path now lives in C# and runs in GitHub Actions.
If you do not want to install the .NET SDK locally, use the `Build and Test` workflow on GitHub:

1. Open the repository on GitHub.
2. Go to the `Build Check` workflow.
3. Click `Run workflow` or open a pull request to trigger it.

That workflow downloads Dalamud on the runner, restores the plugin and test projects, and runs the xUnit suite against the shared MonstersMap search logic and exact coordinate handling.

If you do have the .NET SDK installed, the equivalent local command is:

```bash
dotnet test MonstersMapTests/MonstersMap.Tests.csproj
```

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
