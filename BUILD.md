# Build and Installation Guide

## Prerequisites

- Visual Studio 2022 or Visual Studio Code
- .NET 9.0 SDK
- XIVLauncher with Dalamud installed (API 15+)

## Building the Plugin

### Method 1: Using Visual Studio

1. Open `MonstersMap.csproj` in Visual Studio
2. Restore NuGet packages: `dotnet restore -r win-x64`
3. Build the solution: `dotnet build --configuration Release -r win-x64`

### Method 2: Using Command Line

```powershell
cd MonstersMap
dotnet restore -r win-x64 MonstersMap.csproj
dotnet build --configuration Release -r win-x64
```

## Installation

### For Development

1. Build the plugin (see above)
2. The compiled DLL will be in: `MonstersMap\bin\Release\win-x64\MonstersMap\`
3. Copy the entire `MonstersMap` folder to: `%APPDATA%\XIVLauncher\plugins\`
4. Reload your Dalamud plugins

### For Distribution

1. Build the plugin
2. Copy the `MonstersMap` folder from `bin\Release\win-x64\` to your plugin repository
3. Update `pluginsmaster.json` with the new plugin information
4. Commit and push to your plugin repository

## Usage

Once installed:

1. Open FFXIV and log in
2. Type `/monsters` in the chat
3. Enter a monster name to search
4. Click "Search" or press Enter
5. Select a monster from the results
6. Click "Flag Location" to mark it on your map

## Output Files

After successful build:
- `MonstersMap.dll` - Main plugin DLL
- `MonstersMap.json` - Plugin metadata
- Associated files in the release folder

## Troubleshooting

### Build fails with missing dependencies
- Ensure Dalamud is installed in `%APPDATA%\XIVLauncher\addon\Hooks\dev\`
- Run `dotnet restore` to download dependencies

### Plugin doesn't load
- Check the Dalamud plugin log in `%APPDATA%\XIVLauncher\log\plugin_installers\`
- Ensure the DLL is in the correct plugin folder

### Search doesn't find monsters
- Make sure you're searching for exact or partial monster names
- Some monsters may not be loaded in the object table
- Try different search terms

## GitHub Actions Automated Build

The repository includes workflows for:
- Automated builds on push to main
- Automated deployment to MyDalamudPlugins repository
- Commit message validation

To trigger a build, include `build:` or `build()` in your commit message.

Example:
```
build: Update monster search algorithm
```

## API Version

This plugin is built for:
- **Dalamud API 15+**
- **.NET 9.0**
- Windows 64-bit only
