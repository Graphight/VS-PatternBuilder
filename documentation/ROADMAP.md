# Roadmap

## Completed Features

### Phase 4 Tier 1: Survival Mode Support (2025-12-16)
**Inventory consumption - survival gameplay enabled!**

-  Check player inventory for required blocks before placement
-  Consume items from inventory during placement (server-side authoritative)
-  Handle insufficient materials gracefully (auto-disable, clear messages)
-  Wildcard pattern support (`game:soil-*` matches any variant)
-  Smart consumption (only consumes blocks that actually get placed)
-  Performance optimizations (inventory caching, optimized scanning)
-  Creative mode bypass

### Phase 4 Tier 2: 3D Patterns (2025-12-18)
**Slice-based repeating patterns - periodic variation enabled!**

-  Support patterns with multiple "slices" along direction of travel
-  `Slices` array replaces old `Pattern` field (breaking change)
-  Bidirectional traversal: forward increments, backward decrements slice index
-  Perpendicular movement (turning) maintains current slice
-  Direction tracking with forward direction reference
-  Wrap-around detection prevents duplicate placements on reversal
-  Slice index resets on pattern switch
-  Enables: lamp posts (every Nth block), tunnel supports, decorative alternating patterns, road markers
-  Performance: O(1) slice lookup and direction comparison

### Phase 4 Tier 2: Pattern Preview (2025-12-18)
**Visual preview of pattern placement - prevents mistakes!**

-  Semi-transparent preview blocks rendered 2 blocks ahead of player
-  Color-coded tinting: Green (air), Blue (replacing blocks), Grey (same blocks)
-  Toggle preview on/off via `.pb preview` command
-  Preview updates automatically when building is enabled
-  Supports both 2D and 3D patterns (shows current slice)
-  Client-side mesh rendering with proper texture atlas binding
-  Performance optimized (no debug logging, efficient mesh caching)
-  Carve mode support: shows air blocks as semi-transparent glass
-  Credit: Shader approach inspired by VanillaBuilding: Expanded mod by dsisco

### Phase 4 Tier 2: Directional Block Support (2025-12-21)
**Relative direction directives - reusable patterns in any direction!**

-  Directive syntax: `blockcode|directive1|directive2` (e.g., `game:log-placed|horizontal|f`)
-  Relative directions (`|f|b|l|r`) translate based on player movement (forward/back/left/right)
-  Absolute directions (`|up|down`) for vertical placement
-  Axis hints (`|horizontal|vertical`) for 2-axis blocks like logs
-  Auto-connect directive (`|auto`) for fences/walls (triggers neighbor updates)
-  Supports 2-axis blocks (logs: `-ns`/`-ew` variants)
-  Supports 4-direction blocks (stairs: `-north`/`-south`/`-east`/`-west` variants)
-  Works with wildcards: `cobblestonestairs-*|up|f` (any stone type, facing forward)
-  Preview rendering supports directional blocks
-  On-demand variant resolution using brute-force `SearchBlocks()` with candidates
-  Server-side auto-connect via `TriggerNeighbourBlockUpdate()`

---

## Phase 4 Development Priorities

### Tier 2: Core Usability (Major UX Improvements)
**These make the mod work in real-world scenarios, not just flat creative builds.**

2. **3D patterns** COMPLETE (2025-12-18)
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

3. **Pattern preview** COMPLETE (2025-12-18)
   - Render semi-transparent preview blocks 2 blocks ahead of placement
   - Color-coded tinting system (green/blue/grey)
   - Toggle preview on/off via `.pb preview` command
   - Supports both 2D and 3D patterns with current slice
   - Client-side mesh rendering with texture atlas binding
   - **Impact**: Prevents costly mistakes, provides visual feedback
   - **Complexity**: Medium - client-side rendering, shader setup, texture binding
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
   - Detect when player changes cardinal direction (N=>E, E=>S, etc.)
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

** Item 1 COMPLETE**: Inventory consumption (Tier 1)
-  Survival/multiplayer use enabled
-  Tested with wildcard patterns and smart consumption

** Item 2 COMPLETE**: 3D patterns (Tier 2)
-  Slice-based architecture with bidirectional traversal
-  Breaking change: removed old `Pattern` field
-  Works with wildcard patterns and inventory consumption
-  Tested with lamp post road example

** Item 3 COMPLETE**: Pattern preview (Tier 2)
-  Semi-transparent preview 2 blocks ahead
-  Color-coded tinting (green/blue/grey)
-  Mesh rendering with texture atlas binding
-  Toggle via `.pb preview` command
-  Works with both 2D and 3D patterns
-  Performance optimized (removed debug logging)

**Item 4**: Terrain following (Tier 2)
- Benefits from pattern preview for testing
- Works with both 2D and 3D patterns
- Requires extensive testing on varied terrain

**Item 5 COMPLETE**: Directional block support (Tier 2)
- Relative direction directives (`|f|b|l|r`) translate based on player movement
- Supports 2-axis blocks (logs), 4-direction blocks (stairs), and auto-connect (fences/walls)
- Works with wildcards: `cobblestonestairs-*|up|f`
- Preview rendering fixed to support directional blocks
- On-demand variant resolution using SearchBlocks() with candidate patterns

**Item 6+**: In-game editor (Tier 3 - Nice to have)
- Only tackle after core features are stable
- In-game editor must support 3D pattern creation
- Consider user feedback before prioritizing

**Item 7**: Feedback (Tier 3 - Nice to have)
- Play a little sound when placing the pattern for feedback
- Explore other forms of feedback

**Item 8**: Corners (Tier 3 - Nice to have)
- Programatically make corners or use defined patterns?

### Key Questions to Answer

** Inventory Consumption** (ANSWERED):
-  How to handle patterns with multiple block types? => Track each block type separately, consume all
-  What happens if inventory runs out? => Auto-disable building, show clear message
-  Check entire pattern upfront or per-block? => Check upfront for better UX, cache for 5 placements
-  Creative mode bypass? => Yes, creative mode skips all inventory checks
-  Wildcard support? => Yes, added `game:soil-*` pattern matching
-  Duplicate consumption on existing blocks? => Fixed with smart consumption (check before placing)

**3D Patterns** (ANSWERED):
-  Backwards compatibility: No - breaking change, removed old `Pattern` field entirely
-  What if player walks backwards? => Decrement slice index (bidirectional traversal)
-  Should `.pb info` show current slice index? => Yes, useful for debugging
-  Reset slice index on pattern switch? => Yes, always start from slice 0
-  Max depth limit? => No hard limit, Slices.Length determines depth
-  How to handle validation? => Validate each slice independently, same as 2D patterns
-  How to handle wrap-around on reversal? => Detect index 0, double-decrement to prevent duplicates
-  Direction tracking? => Track forward direction, update on perpendicular turns

**Terrain Following**:
- Max climb/drop per segment? (suggest 2-3 blocks)
- How to handle caves/overhangs? (raycast from player height downward)
- Should bridges auto-span gaps? (phase 5 feature, not initial implementation)
- Stairs vs ramps on slopes? (use pattern definition, don't auto-generate)

**Pattern Preview** (ANSWERED):
- Preview distance ahead? => 2 blocks ahead for better visibility
- Preview while disabled? => No, only when building enabled
- Performance impact of rendering? => Minimal after debug log removal
- Tinting system? => Green (air), Blue (replacing), Grey (same blocks)
- Carve mode support? => Yes, shows air blocks as semi-transparent glass
- Texture binding? => Block texture atlas bound to shader for correct textures

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
