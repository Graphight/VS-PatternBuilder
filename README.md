# PatternBuilder - Automated Pattern Placement for Vintage Story

A Vintage Story mod that automates placement of repeating block patterns (roads, walls, tunnels) to reduce late-game construction tedium.

## Status

**Phase 1**: ✅ Complete - Basic road placement prototype

**Phase 2**: ✅ Complete - Multi-pattern system with JSON configs

**Phase 3**: ✅ Complete - Advanced features (carve mode, validation, performance)

**Phase 4 Tier 1**: ✅ Complete - Survival mode support with inventory consumption

**Phase 4 Tier 2**: ✅ Complete - 3D patterns (slice-based repeating patterns) + Pattern preview

**Phase 4 Tier 3**: Planned - Terrain following, directional blocks, in-game editor

## Features

- **Pattern-based building**: Define custom block patterns in JSON files
- **50 pattern slots**: Quick-switch between different patterns (configurable)
- **Movement-based placement**: Walk to build - patterns follow your movement
- **Directional awareness**: Patterns orient based on movement direction (N/S/E/W)
- **3D slice patterns**: Multi-slice patterns with bidirectional traversal for periodic variation (lamp posts, markers, etc.)
- **Pattern preview**: See semi-transparent preview blocks 2 positions ahead with color-coded tinting (green=air, blue=replacing, grey=same)
- **Adaptive & Carve modes**: Patterns can mold to terrain or carve through it
- **Survival mode support**: Consumes blocks from inventory, works in both creative and survival
- **Wildcard patterns**: Match any block variant (e.g., `game:soil-*` matches all soil types)
- **Smart consumption**: Only uses materials for blocks that actually get placed
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
.pb preview      Toggle pattern preview on/off
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

### Example Patterns

**Basic 2D pattern with exact block types**:
```json
{
  "Name": "Default Road",
  "Description": "3-wide gravel road with dirt foundation",
  "Slices": [ "DDD,GGG,_P_,___" ],
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

**3D pattern with multiple slices** (lamp posts every 8 blocks):
```json
{
  "Name": "Lamp Post Road",
  "Description": "3-wide gravel road with lamp posts every 8 blocks",
  "Slices": [
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,_P_,___,___",
    "DDD,GGG,WPW,W_W,L_L"
  ],
  "Width": 3,
  "Height": 5,
  "Blocks": {
    "D": "game:soil-medium-normal",
    "G": "game:gravel-granite",
    "W": "game:woodenfence-oak-empty-free",
    "L": "game:paperlantern-on",
    "P": "player"
  }
}
```

**Wildcard pattern** (survival-friendly):
```json
{
  "Name": "Flexible Road",
  "Description": "Works with ANY soil and gravel variants",
  "Slices": [ "SSSSS,GGGGG,__P__,_____" ],
  "Width": 5,
  "Height": 4,
  "Mode": "adaptive",
  "Blocks": {
    "S": "game:soil-*",
    "G": "game:gravel-*",
    "P": "player"
  }
}
```

**Pattern syntax**:
- Slices: Array of 2D patterns (one entry = 2D pattern, multiple entries = 3D pattern)
- Each slice uses comma-separated Y-layers (bottom to top)
- Each character maps to a block code
- `_` = air/empty
- `P` = player feet position (required in each slice for Y-offset)
- `*` = wildcard (matches any variant, e.g., `game:soil-*`)
- `Mode` = "adaptive" (molds to terrain) or "carve" (cuts through terrain)

**3D pattern behavior**:
- Walk forward: cycles through slices (0→1→2...→N→0)
- Walk backward: reverses through slices (N→...→2→1→0→N)
- Turn left/right: maintains current slice (no slice change)
- Pattern switch: resets to slice 0

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

**Creative Mode**:
1. Type `.pb on` to enable building mode
2. Walk forward - patterns will be placed ahead of you
3. Switch patterns with `.pb slot <number>`
4. No materials consumed from inventory

**Survival Mode**:
1. Gather blocks needed for your pattern (check with `.pb info`)
2. Type `.pb on` to enable building mode
3. Walk forward - blocks consumed from inventory as you build
4. Use wildcard patterns (`game:soil-*`) for flexibility
5. Auto-disables when out of materials (shows clear message)

**Tips**:
- Edit pattern JSON files in the config directory
- Reload patterns with `.pb reload` after editing
- Use `.pb info` to see current pattern details and material requirements
- Walking over existing patterns won't waste materials
- Enable preview with `.pb preview` to see what will be placed before it happens

**Pattern Preview**:
- Preview appears 2 blocks ahead of your movement
- **Green tint**: Placing in air (safe)
- **Blue tint**: Replacing existing blocks (intentional)
- **Grey tint**: Replacing with same block (no extra consumption)
- Works with both 2D and 3D patterns (shows current slice)
- Toggle on/off anytime with `.pb preview`

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

See [documentation/ROADMAP.md](documentation/ROADMAP.md) for active issues and backlog.

## Troubleshooting

**Mod doesn't load**:
- Check `modinfo.json` is valid JSON
- Verify Vintage Story version compatibility (1.21.0+)
- Check for compilation errors in build output

**Blocks don't place**:
- Check Vintage Story console for block loading errors
- Ensure pattern files use valid block codes (e.g., `game:gravel-granite`)
- In survival mode: Verify you have required materials in inventory
- Use `.pb info` to see what materials are needed

**Pattern edits not working**:
- Use `.pb reload` command to reload patterns from disk
- Check pattern JSON syntax is valid
- Verify file is named correctly (`slotN_name.json`)

**Debug logs**: Check `VintagestoryData/Logs/` for detailed error messages. Mod messages are prefixed with `[PatternBuilder]`.

## Development

- Built for Vintage Story 1.21.0+

## License

Standard [MIT license](./LICENSE)