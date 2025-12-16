# Roadmap

## Phase 4 Development Priorities

### Tier 1: Critical for Survival/Multiplayer (Blocks Adoption)
**Without these, the mod is creative-mode only and unusable on most servers.**

1. **Inventory consumption** (survival mode support)
   - Check player inventory for required blocks before placement
   - Consume items from inventory during placement
   - Handle insufficient materials gracefully (stop building, notify player)
   - Block-by-block consumption (not pattern-by-pattern)
   - Consider hotbar vs full inventory search
   - **Impact**: Enables survival gameplay and balanced multiplayer use
   - **Complexity**: Medium - inventory API, item matching, edge cases

### Tier 2: Core Usability (Major UX Improvements)
**These make the mod work in real-world scenarios, not just flat creative builds.**

2. **3D patterns** (slice-based repeating patterns)
   - Support patterns with multiple "slices" along direction of travel
   - Add optional `Slices` array to pattern JSON (backwards compatible)
   - Track slice index during placement, cycle through slices
   - Reset slice index on pattern switch
   - Enables: lamp posts (light every Nth block), tunnel supports, decorative alternating patterns, road markers
   - **Impact**: Fundamental pattern capability - unlocks creative designs like periodic variation
   - **Complexity**: Medium - pattern parsing, slice cycling logic, backwards compatibility
   - **Risk**: Low - additive feature, doesn't break existing 2D patterns

   **Example pattern**:
   ```json
   {
     "Name": "Lamp Post Road",
     "Width": 3,
     "Height": 4,
     "Depth": 8,
     "Slices": [
       "DDD,GGG,_P_,___",  // Repeat 7 times
       "DDD,GGG,_P_,___",
       "DDD,GGG,_P_,___",
       "DDD,GGG,_P_,___",
       "DDD,GGG,_P_,___",
       "DDD,GGG,_P_,___",
       "DDD,GGG,_P_,___",
       "DDD,GGG,LPL,___"   // Lamp post on 8th slice
     ],
     "Blocks": {
       "D": "game:soil-medium-normal",
       "G": "game:gravel-granite",
       "L": "game:lantern-iron-on",
       "P": "player"
     }
   }
   ```

3. **Pattern preview** (ghost blocks before placement)
   - Render semi-transparent preview blocks at next placement position
   - Update preview based on movement direction
   - Toggle preview on/off via command
   - Preview should respect terrain (once terrain following is implemented)
   - Preview should show correct slice in 3D patterns
   - **Impact**: Prevents costly mistakes, provides visual feedback
   - **Complexity**: Medium - client-side rendering, temporary blocks
   - **Risk**: Low - purely visual, no gameplay impact

4. **Terrain following** (raycasting for ground level)
   - Raycast downward from pattern position to find ground
   - Adjust Y-offset dynamically based on terrain height
   - Handle gaps (bridges) vs hills intelligently
   - Configurable max drop/climb per segment
   - **Impact**: Makes mod work on natural terrain, not just flat ground
   - **Complexity**: Medium-High - raycasting API, Y-offset logic, edge cases
   - **Risk**: Medium - could conflict with existing placement logic

### Tier 3: Advanced Features (Nice to Have)
**Polish and power-user features that enhance but don't fundamentally change usage.**

5. **Corner detection** (automatic pattern rotation on direction changes)
   - Detect when player changes cardinal direction (N→E, E→S, etc.)
   - Place special corner patterns at turn points
   - Corner pattern slots (e.g., slot 51-54 for NE, SE, SW, NW corners)
   - Fallback to regular pattern if corner pattern missing
   - **Impact**: Enables complex path layouts without manual switching
   - **Complexity**: High - direction change detection, corner pattern management, rotation
   - **Risk**: Medium - could introduce placement bugs at intersections

6. **In-game pattern editor** (GUI-based pattern creation)
   - Visual grid editor for pattern design
   - Block picker from game blocks
   - Live preview of pattern
   - Save/load to pattern slots
   - Import/export pattern JSON
   - Must support 3D patterns (slice editor)
   - **Impact**: Lowers barrier to entry for non-technical users
   - **Complexity**: High - full GUI system, pattern validation, file I/O, 3D slice management
   - **Risk**: Low - doesn't affect core placement logic

### Tier 4: Polish & Community Features
**Deferred until core functionality is solid.**

7. **Pattern export/sharing system**
   - Copy pattern JSON to clipboard
   - Import patterns from clipboard or URL
   - Pattern library browser (community patterns)
   - **Complexity**: Medium-High - depends on implementation approach

8. **Undo system**
   - Store last N pattern placements
   - `.pb undo` command to remove last placement
   - Configurable undo history depth
   - **Complexity**: Medium - placement history tracking, bulk block removal

## Implementation Notes

### Recommended Order
Based on dependencies and impact:

**Sprint 1**: Inventory consumption (Tier 1)
- Unblocks survival/multiplayer use
- No dependencies on other features
- Test on survival worlds and multiplayer servers

**Sprint 2**: 3D patterns (Tier 2)
- Fundamental pattern capability
- Relatively simple implementation (1-2 hours)
- Backwards compatible with existing 2D patterns
- Enables creative designs immediately

**Sprint 3**: Pattern preview (Tier 2)
- Provides visual feedback for testing terrain following
- Must support 3D pattern slice preview
- Helps validate inventory consumption and 3D patterns

**Sprint 4**: Terrain following (Tier 2)
- Benefits from pattern preview for testing
- Works with both 2D and 3D patterns
- Requires extensive testing on varied terrain

**Sprint 5+**: Corners, then in-game editor (Tier 3)
- Only tackle after core features are stable
- In-game editor must support 3D pattern creation
- Consider user feedback before prioritizing

### Key Questions to Answer

**Inventory Consumption**:
- How to handle patterns with multiple block types? (consume proportionally)
- What happens mid-pattern if inventory runs out? (stop gracefully, don't place partial)
- Should it check entire pattern upfront or per-block? (per-block for better UX)
- Creative mode bypass or always consume? (bypass in creative)

**3D Patterns**:
- Backwards compatibility: fallback to `Pattern` string if `Slices` absent? (yes)
- What if player walks backwards? (always increment slice index - simpler)
- Should `.pb info` show current slice index? (yes, useful for debugging)
- Reset slice index on pattern switch? (yes, always start from slice 0)
- Max depth limit? (suggest 100 slices, prevents accidental massive patterns)
- How to handle validation? (validate each slice independently, same as 2D patterns)

**Terrain Following**:
- Max climb/drop per segment? (suggest 2-3 blocks)
- How to handle caves/overhangs? (raycast from player height downward)
- Should bridges auto-span gaps? (phase 5 feature, not initial implementation)
- Stairs vs ramps on slopes? (use pattern definition, don't auto-generate)

**Pattern Preview**:
- Preview distance ahead? (1 pattern placement, same as actual placement)
- Preview while disabled? (no, only when building enabled)
- Performance impact of rendering? (should be minimal, single pattern per tick)

**Corners**:
- Separate corner pattern slots or auto-rotate? (separate slots for flexibility)
- How tight can corners be? (minimum 1 block, detect on direction change)
- What about U-turns? (treat as 2 corners)

**In-game Editor**:
- Replace JSON or supplement? (supplement - JSON should remain primary)
- Pattern size limits? (match current system - reasonable bounds like 10x10x100)
- Block search/filter? (essential for usability)
- How to edit 3D patterns? (slice-by-slice editor with navigation, copy/paste slices)
- Preview while editing? (yes, essential for 3D pattern visualization)

# Bugs

## Active
None currently!

## Fixed
- ~~When sprinting some placements are missed~~ - Fixed with 100ms tick rate and 0.6 block threshold
- ~~All patterns build below the player's feet~~ - Working as designed; P-marker in pattern controls vertical positioning
- ~~Invalid block codes fail silently~~ - Fixed with pattern validation and chat warnings
- ~~The reload command crashes the game~~ - Fixed by clean DLL reinstall
