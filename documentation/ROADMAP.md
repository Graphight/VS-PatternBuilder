# Roadmap

## Planned Features

### Tier 2: Core Usability (Major UX Improvements)
**These make the mod work in real-world scenarios, not just flat creative builds.**

1. **Terrain following** (raycasting for ground level)
   - Raycast downward from pattern position to find ground
   - Adjust Y-offset dynamically based on terrain height
   - Handle gaps (bridges) vs hills intelligently
   - Configurable max drop/climb per segment
   - **Impact**: Makes mod work on natural terrain, not just flat ground
   - **Complexity**: Medium-High - raycasting API, Y-offset logic, edge cases
   - **Risk**: Medium - could conflict with existing placement logic

### Tier 3: Advanced Features (Nice to Have)
**Polish and power-user features that enhance but don't fundamentally change usage.**

2. **Tool durability use** (survival only)
   - When "carving" the mod eats away at relevant tool durability
   - dirt/gravel uses shovel rocks use pickaxe
   - Configurable via mod settings

3. **In-game pattern editor** (GUI-based pattern creation)
   - Visual grid editor for pattern design
   - Block picker from game blocks
   - Live preview of pattern
   - Save/load to pattern slots
   - Import/export pattern JSON
   - Must support 3D patterns (slice editor)

### Tier 4: Polish & Community Features
**Deferred until core functionality is solid.**

4. **Pattern export/sharing system**
   - Copy pattern JSON to clipboard
   - Import patterns from clipboard or URL
   - Pattern library browser (community patterns)
   - **Complexity**: Medium-High - depends on implementation approach

5. **Undo system**
   - Store last N pattern placements
   - `.pb undo` command to remove last placement
   - Configurable undo history depth
   - **Complexity**: Medium - placement history tracking, bulk block removal

--- 

### Key Questions to Answer

**Terrain Following**:
- Max climb/drop per segment? (suggest 2-3 blocks)
- How to handle caves/overhangs? (raycast from player height downward)
- Should bridges auto-span gaps? (phase 5 feature, not initial implementation)
- Stairs vs ramps on slopes? (use pattern definition, don't auto-generate)

**In-game Editor**:
- Replace JSON or supplement? (supplement - JSON should remain primary)
- Pattern size limits? (match current system - reasonable bounds like 10x10x100)
- Block search/filter? (essential for usability)
- How to edit 3D patterns? (slice-by-slice editor with navigation, copy/paste slices)
- Preview while editing? (yes, essential for 3D pattern visualization)

---

# Known Bugs

## Active
- When given an asymmetrical hoizontal pattern the system still thinks the player is in the center
- Validation is skipped for blocks with wildcards ('*') which means players can put garbage in there

## Fixed
- ~~When sprinting some placements are missed~~ - Fixed with 100ms tick rate and 0.6 block threshold
- ~~All patterns build below the player's feet~~ - Working as designed; P-marker in pattern controls vertical positioning
- ~~Invalid block codes fail silently~~ - Fixed with pattern validation and chat warnings
- ~~The reload command crashes the game~~ - Fixed by clean DLL reinstall
- ~~Preview mode doesn't work for directional blocks~~ - Fixed by adding DirectionalBlockResolver.ResolveBlockId() to PreviewManager
