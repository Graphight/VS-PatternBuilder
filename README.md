# PatternBuilder - Automated Pattern Placement for Vintage Story

A Vintage Story mod that automates placement of repeating block patterns (roads, walls, tunnels) to reduce late-game construction tedium.

## Status

**Phase 1**: Complete - Basic road placement prototype

**Phase 2**: Complete - Multi-pattern system with JSON configs

**Phase 3**: In Progress - Advanced features (carve mode, validation, performance)

**Phase 4**: Planned - In-game pattern editor

## Features

- **Pattern-based building**: Define custom block patterns in JSON files
- **50 pattern slots**: Quick-switch between different patterns (configurable)
- **Movement-based placement**: Walk to build - patterns follow your movement
- **Directional awareness**: Patterns orient based on movement direction (N/S/E/W)
- **Adaptive & Carve modes**: Patterns can mold to terrain or carve through it
- **Hot-reload patterns**: Edit patterns while game is running
- **Pattern validation**: Automatic validation with helpful error messages
- **Persistent placement**: Blocks remain after world reload
- **VS recipe syntax**: Familiar pattern format for Vintage Story modders

## Commands

```
.pb              Show command help (default)
.pb help         Show command help
.pb toggle       Toggle pattern building on/off
.pb on/off       Enable/disable building mode
.pb list         Show available patterns
.pb slot <X>     Switch to pattern at slot <X> (1-50)
.pb info         Show current pattern details
.pb reload       Reload patterns from disk
```

## Pattern Files

Patterns are stored in JSON files at:

**Mac OS**:
```
~/Library/Application Support/VintagestoryData/ModConfig/patternbuilder/patterns/
```

**Windows**:
```
%APPDATA%\VintagestoryData\ModConfig\patternbuilder\patterns\
```

**File naming**: Pattern files must be named `slotN_name.json` where N is 1-50 (e.g., `slot1_road.json`, `slot2_path.json`).

### Example Pattern

```json
{
  "Name": "Default Road",
  "Description": "3-wide gravel road with dirt foundation",
  "Pattern": "DDD,GGG,_P_,___",
  "Width": 3,
  "Height": 4,
  "Mode": "adaptive",
  "Blocks": {
    "D": "game:soil-medium-normal",
    "G": "game:gravel-granite",
    "P": "player"
  }
}
```

- Pattern uses comma-separated Y-layers (bottom to top)
- Each character maps to a block code
- `_` = air/empty
- `P` = player feet position (required for Y-offset)
- `Mode` = "adaptive" (molds to terrain) or "carve" (cuts through terrain)

## Installation

1. Download or build the mod
2. Place `patternbuilder` folder in your VintagestoryData/Mods directory
3. Launch Vintage Story
4. Default patterns will be created automatically on first run

## Building from Source

**Quick build**:

**Mac OS / Linux**:
```bash
./build.sh
```

**Windows**:
```powershell
build.sh
```

The build script validates JSON files, compiles the mod, and packages it in `Releases/patternbuilder/`.

**Deploy to Vintage Story**:

**Mac OS**:
```bash
cp -r Releases/patternbuilder ~/Library/Application\ Support/VintagestoryData/Mods/
```

**Windows**:
```powershell
Copy-Item -Recurse -Force Releases/patternbuilder $env:APPDATA\VintagestoryData\Mods\
```

**Manual build**:
```bash
dotnet build PatternBuilder/PatternBuilder.csproj -c Release
dotnet clean  # Clean build artifacts
```

**Hot-reload workflow**: After building, reload your world in Vintage Story (don't restart the game) and code changes take effect immediately. Some changes (like ModSystem initialization) may require a full game restart.

## Usage

1. Enter a creative world
2. Type `.pb on` to enable building mode
3. Walk forward - patterns will be placed ahead of you
4. Switch patterns with `.pb slot <number>`
5. Edit pattern JSON files in the config directory
6. Reload patterns with `.pb reload`

## Pattern Modes

**Adaptive Mode** (default):
- Patterns mold to existing terrain
- Only places solid blocks, skips air
- Ideal for roads, paths, and decorative patterns

**Carve Mode**:
- Patterns cut through terrain
- Places air blocks to clear space
- Ideal for tunnels and underground structures

Set mode in pattern JSON:
```json
{
  "Mode": "carve",  // or "adaptive"
  ...
}
```

## Known Issues

See [documentation/known_issues.md](documentation/known_issues.md) for active issues and backlog.

## Troubleshooting

**Mod doesn't load**:
- Check `modinfo.json` is valid JSON
- Verify Vintage Story version compatibility (1.21.0+)
- Check for compilation errors in build output

**Blocks don't place**:
- Verify you're in Creative mode (required for block placement)
- Check Vintage Story console for block loading errors
- Ensure pattern files use valid block codes (e.g., `game:gravel-granite`)

**Pattern edits not working**:
- Use `.pb reload` command to reload patterns from disk
- Check pattern JSON syntax is valid
- Verify file is named correctly (`slotN_name.json`)

**Debug logs**: Check `VintagestoryData/Logs/` for detailed error messages. Mod messages are prefixed with `[PatternBuilder]`.

## Development

- Built for Vintage Story 1.21.0+
- Uses C# with VS Modding API
- Hot-reload capable for rapid iteration

## License

Standard [MIT license](./LICENSE)