# BuilderRoads - Automated Pattern Placement for Vintage Story

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
.road              Show command help
.road toggle       Toggle road building on/off
.road on/off       Enable/disable building mode
.road list         Show available patterns
.road slot <1-5>   Switch to pattern slot
.road reload       Reload patterns from disk
```

## Pattern Files

Patterns are stored in JSON files at:
```
~/Library/Application Support/VintagestoryData/ModConfig/builderroads/patterns/
```

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
2. Place `builderroads` folder in your VintagestoryData/Mods directory
3. Launch Vintage Story
4. Default patterns will be created automatically on first run

## Building from Source

```bash
./build.sh
```

See [DEVELOPMENT.md](DEVELOPMENT.md) for detailed build instructions.

## Usage

1. Enter a creative world
2. Type `.road on` to enable building mode
3. Walk forward - patterns will be placed ahead of you
4. Switch patterns with `.road slot <number>`
5. Edit pattern JSON files in the config directory
6. Reload patterns with `.road reload`

## Known Issues

- Sprinting may cause some placements to be missed
- All patterns currently build below player feet (walls/tunnels need placement mode system)

See [documentation/known_issues.md](documentation/known_issues.md) for full list and backlog.

## Development

- Built for Vintage Story 1.21.1+
- Uses C# with VS Modding API
- Hot-reload capable for rapid iteration

## License

Standard [MIT license](./LICENSE)