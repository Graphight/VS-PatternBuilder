# Roadmap

## Planned Features

### Tier 2: Core Usability (Major UX Improvements)
**These make the mod work in real-world scenarios, not just flat creative builds.**

1. **Terrain following** (raycasting for ground level) - ✅ **MOSTLY COMPLETE**
   - ✅ Raycast downward from pattern position to find ground (2 blocks ahead)
   - ✅ Adjust Y-offset dynamically based on terrain height
   - ✅ Transition layers for stairs (TransitionUpLayer/TransitionDownLayer)
   - ✅ Foliage filtering (ignores trees, plants, decorative blocks)
   - ✅ Ascending stairs (works perfectly)
   - ✅ Descending stairs (hybrid approach - works at walking speed)
   - ✅ Dynamic tick rates (50ms descending, 100ms normal)
   - ⚠️ **Known limitation**: Sprinting downhill skips 20-30% of stairs (acceptable)
   - 🔄 **Remaining work**: Edge case testing (cliffs, caves, water), transition layer validation
   - **Status**: Core functionality complete (v0.4.5), polish/edge cases deferred to user feedback

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

**Terrain Following** (mostly answered):
- ~~Max climb/drop per segment?~~ **ANSWER**: One block at a time (via transition patterns)
- ~~How to handle caves/overhangs?~~ **ANSWER**: Raycast from lookahead position downward
- ~~Stairs vs ramps on slopes?~~ **ANSWER**: Use pattern definition (TransitionUpLayer/TransitionDownLayer)
- Should bridges auto-span gaps? **ANSWER**: Use "carve" mode instead as this will keep y-level

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
- No validation that TransitionUpLayer/TransitionDownLayer exist when terrain following is enabled
- Sprinting down slopes will skip some transition layer placements (I spent way too long trying to fix this so gave up)

## Fixed
- ~~When sprinting some placements are missed~~ - Fixed with 100ms tick rate and 0.6 block threshold
- ~~All patterns build below the player's feet~~ - Working as designed; P-marker in pattern controls vertical positioning
- ~~Invalid block codes fail silently~~ - Fixed with pattern validation and chat warnings
- ~~The reload command crashes the game~~ - Fixed by clean DLL reinstall
- ~~Preview mode doesn't work for directional blocks~~ - Fixed by adding DirectionalBlockResolver.ResolveBlockId() to PreviewManager
- ~~Trees and plants cause false elevation changes~~ - Fixed with material-based foliage filtering (v0.4.5)
- ~~Descending stairs don't place~~ - Fixed with Option B hybrid approach (v0.4.5)

## Known Limitations (Acceptable - Documented)
- **Sprinting downhill**: Skips 20-30% of descending stairs when sprinting (works perfectly at walking speed)
  - **Why**: Tick rate (50ms) can't catch all Y-changes at sprint speed (~7-8 blocks/sec)
  - **Mitigation**: Walk (don't sprint) when descending for best results
  - **Status**: Documented in README, won't fix (over-engineering for edge case)
