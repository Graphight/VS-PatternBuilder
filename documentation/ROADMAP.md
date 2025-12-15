# Known Issues

## Active Bugs
None currently!

## Backlog / UX Improvements
- Pattern preview mode (show ghost blocks before placement)
- Terrain following (raycasting to follow ground level)
- In-game pattern editor
- Corner detection for directional changes
- Pattern export/sharing system

## Fixed
- ~~When sprinting some placements are missed~~ - Fixed with 100ms tick rate and 0.6 block threshold
- ~~All patterns build below the player's feet~~ - Working as designed; P-marker in pattern controls vertical positioning
- ~~Invalid block codes fail silently~~ - Fixed with pattern validation and chat warnings
- ~~The reload command crashes the game~~ - Fixed by clean DLL reinstall