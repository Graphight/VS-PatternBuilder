# BuilderRoads - Development Guide

## Quick Start

### Build and Deploy
```bash
# MacOS
./build.sh && cp -r Releases/builderroads ~/Library/Application\ Support/VintagestoryData/Mods/
```

This script:
1. Validates JSON files (modinfo.json)
2. Cleans and builds the Release configuration
3. Packages the mod
4. Copies to Vintage Story mods directory

**Alternative**: Manual build
```bash
dotnet build BuilderRoads/BuilderRoads.csproj -c Release
```

### Development Workflow

1. **Edit code** - Make changes to `.cs` files in `BuilderRoads/`
2. **Build & deploy** - Run `./build.sh`
3. **Test in-game** - Reload the world in Vintage Story (hot-reload supported)
4. **Debug** - Check VS console output and in-game chat messages

### Project Structure

```
BuilderRoads/
├── BuilderRoads.csproj          # Project file
├── modinfo.json                 # Mod metadata
├── BuilderRoadsModSystem.cs     # Main mod entry point
├── PatternDefinition.cs         # Pattern data structure (Phase 2)
├── PatternLoader.cs             # JSON loading (Phase 2 - TBD)
└── PatternManager.cs            # Pattern switching (Phase 2 - TBD)
```

## Testing Checklist

### Phase 1 (Complete)
- [X] Mod loads without errors in VS console
- [X] Ctrl+Shift+R toggles road building mode
- [X] Chat notifications confirm toggle state
- [X] Walking forward places 3x3 gravel road
- [X] Road follows player direction (N/S/E/W)
- [X] Road appears 1 block ahead of player
- [X] No crashes during extended use

### Phase 2 (In Progress)
- [X] Pattern system uses PatternDefinition internally
- [X] Block caching works for pattern blocks
- [ ] Pattern placement respects width/height
- [ ] JSON pattern loading from config files
- [ ] Pattern switching via number keys (1-5)
- [ ] Invalid patterns fail gracefully

## Useful Commands

### Build for specific configuration
```bash
dotnet build -c Debug    # Debug build
dotnet build -c Release  # Release build (default for ./build.sh)
```

### Clean build
```bash
dotnet clean
./build.sh
```

### Check build output
The built mod is located at:
```
BuilderRoads/bin/Release/Mods/mod/
```

## Hot-Reload

Vintage Story supports hot-reloading of code mods:
1. Build the mod (`./build.sh`)
2. In VS, reload the world (don't restart the game)
3. Code changes take effect immediately

**Note**: Some changes (like ModSystem initialization) may require a full game restart.

## Debugging

### Console Output
- VS logs mod messages to the game console
- Look for lines starting with `[BuilderRoads]`

### In-Game Debugging
- Chat notifications show mod state changes
- Pattern placement shows directional feedback
- Check VS logs at: `VintagestoryData/Logs/`

### Common Issues

**Mod doesn't load**:
- Check `modinfo.json` is valid JSON
- Verify VS version compatibility (1.21.1+)
- Check for compilation errors in build output

**Blocks don't place**:
- Verify you're in Creative mode
- Check block IDs cached successfully (console logs)
- Ensure pattern validation passed on startup

**Hot-reload doesn't work**:
- Try a full game restart
- Verify mod file was actually updated (check timestamp)
- Check for errors in VS console

## Git Workflow

### Before committing
```bash
./build.sh              # Ensure it builds
# Test in-game
git add .
git commit -m "Descriptive message"
```

### Commit Message Format
Follow project spec conventions:
- `feat: Add pattern switching hotkeys`
- `fix: Correct block placement offset`
- `refactor: Extract pattern validation`
- `docs: Update development guide`

## References

- [Vintage Story Modding API](https://apidocs.vintagestory.at/)
- [VS Modding Wiki](https://wiki.vintagestory.at/Modding:Overview)
- [Project Specification](.claude/project_spec.md)