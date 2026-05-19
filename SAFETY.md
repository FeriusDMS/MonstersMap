# MonstersMap - Project Structure and Safety Documentation

## Project Structure

```
MonstersMap/
├── .github/
│   └── workflows/
│       ├── commit_norm_check.yml   # Validates commit messages
│       └── release.yml              # Automated build and deploy workflow
├── MonstersMap/
│   ├── MonstersMap.cs              # Main plugin code
│   ├── MonstersMap.csproj          # Project configuration (Dalamud API 15)
│   ├── MonstersMap.json            # Plugin metadata
│   └── packages.lock.json          # Dependency lock file
├── stylecop.json                   # Code style configuration
├── .gitignore                      # Git ignore rules
├── BUILD.md                        # Build and installation guide
├── SAFETY.md                       # Security compliance document
├── README.md                       # User documentation
└── LICENSE                         # MIT License
```

## Architecture Overview

### Plugin Components

1. **Plugin Class** (`IDalamudPlugin`)
   - Initializes the plugin on load
   - Manages command registration (`/monsters`)
   - Handles UI rendering lifecycle
   - Gracefully disposes resources

2. **MonstersMapWindow Class** (Dalamud `Window`)
   - ImGui-based UI for monster search
   - Displays search results with distance calculations
   - Flag placement interface
   - Client-side only operations

3. **FlaggedMonster Struct**
   - Stores current flagged monster information
   - Contains position and zone data
   - Used for display and reference

### Service Dependencies

```csharp
ICommandManager        // For /monsters command
IObjectTable          // For monster detection (read-only)
IPluginLog           // For logging
IGameGui             // For UI integration
IClientState         // For player position and zone info
IDalamudPluginInterface // Plugin lifecycle management
```

## Security and Compliance

### ✅ Client-Side Only

- **No Server Communication**: All operations performed locally
- **Read-Only Access**: Only reads from ObjectTable, no modifications
- **No Data Transmission**: No personal data, game data, or telemetry sent
- **Local Flags Only**: Flags stored in memory, not persistent on disk

### ✅ Safe API Usage

- Only uses public Dalamud API 15 services
- No unsafe operations beyond those required by the framework
- No game memory manipulation
- No packet inspection or modification

### ✅ Code Safety

- **Exception Handling**: All operations wrapped in try-catch
- **Null Checking**: Safe null handling throughout
- **Resource Cleanup**: Proper disposal of resources in Dispose()
- **String Sanitization**: Safe string operations with bounds checking

## Compliance with Dalamud Guidelines

1. ✅ **Plugin Interface Implementation**: Properly implements `IDalamudPlugin`
2. ✅ **Service Injection**: Uses correct `[PluginService]` attributes
3. ✅ **Logging**: Uses provided `IPluginLog` service
4. ✅ **UI Standards**: Uses Dalamud's `WindowSystem` for UI management
5. ✅ **Command Registration**: Proper command handling and cleanup
6. ✅ **Resource Management**: Proper disposal in Dispose() method

## No Risks Involved

- ❌ **No Ban Risk**: No server-side modifications or cheating
- ❌ **No Game Modification**: Pure client-side reading
- ❌ **No Malware**: Open source, no external dependencies
- ❌ **No Privacy Issues**: No data collection or transmission

## Testing Checklist

- [ ] Plugin loads without errors
- [ ] `/monsters` command opens window
- [ ] Monster search works in current zone
- [ ] Distance calculation is accurate
- [ ] Flag placement displays monster info
- [ ] Clearing flag removes information
- [ ] Plugin UI is responsive
- [ ] No errors in Dalamud log
- [ ] No memory leaks over time

## Development Notes

- Built with **Dalamud SDK 15.0.0**
- Targets **.NET 9.0-windows**
- Nullable reference types enabled
- Allows unsafe blocks for interop if needed
- Uses StyleCop analyzers for code quality

## Future Enhancement Ideas

- [ ] Persistent flag save/load
- [ ] Map overlay with visual markers
- [ ] Hunt list integration
- [ ] Favorite monsters list
- [ ] Coordinates sharing (clipboard)
- [ ] Multiple zone support
- [ ] Monster database
- [ ] Web API for updates
