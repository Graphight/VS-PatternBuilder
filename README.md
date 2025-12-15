# PatternBuilder - Automated Pattern Placement for Vintage Story

A Vintage Story mod that automates placement of repeating block patterns (roads, walls, tunnels) to reduce late-game construction tedium.

## Status

**Phase 1**: Complete - Basic road placement prototype
**Phase 2**: Complete - Multi-pattern system with JSON configs
**Phase 3**: Planned - Advanced features (terrain following, pattern editor)

## Features

- **Pattern-based building**: Define custom block patterns in JSON files
- **5 pattern slots**: Quick-switch between different patterns
- **Movement-based placement**: Walk to build - patterns follow your movement
- **Directional awareness**: Patterns orient based on movement direction (N/S/E/W)
- **Hot-reload patterns**: Edit patterns while game is running
- **Persistent placement**: Blocks remain after world reload
- **VS recipe syntax**: Familiar pattern format for Vintage Story modders

## Commands

```
.pb              Show command help
.pb toggle       Toggle pattern building on/off
.pb on/off       Enable/disable building mode
.pb list         Show available patterns
.pb slot <1-5>   Switch to pattern slot
.pb reload       Reload patterns from disk
```

## Pattern Files

Patterns are stored in JSON files at:
```
~/Library/Application Support/VintagestoryData/ModConfig/patternbuilder/patterns/
```

**File naming**: Pattern files must be named `slotN_name.json` where N is 1-5 (e.g., `slot1_road.json`, `slot2_path.json`).

### Example Pattern

```json
{
  "Name": "Default Road",
  "Description": "3-wide gravel road with dirt foundation",
  "Pattern": "DDD,GGG,_P_,___",
  "Width": 3,
  "Height": 4,
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

## Installation

1. Download or build the mod
2. Place `patternbuilder` folder in your VintagestoryData/Mods directory
3. Launch Vintage Story
4. Default patterns will be created automatically on first run

## Building from Source

**Quick build**:
```bash
./build.sh
```

The build script validates JSON files, compiles the mod, and packages it in `Releases/patternbuilder/`.

**Deploy to Vintage Story (macOS)**:
```bash
cp -r Releases/patternbuilder ~/Library/Application\ Support/VintagestoryData/Mods/
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

## Known Issues

- Sprinting may cause some placements to be missed
- All patterns currently build below player feet (walls/tunnels need placement mode system)

See [documentation/known_issues.md](documentation/known_issues.md) for full list and backlog.

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