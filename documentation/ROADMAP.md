# Roadmap

## Planned Features

### Tier 3: Advanced Features (Nice to Have)
**Polish and power-user features that enhance but don't fundamentally change usage.**

1. **In-game pattern editor** (GUI-based pattern creation)
   - Visual grid editor for pattern design
   - Block picker from game blocks
   - Live preview of pattern
   - Save/load to pattern slots
   - Import/export pattern JSON
   - Must support 3D patterns (slice editor)

### Tier 4: Polish & Community Features
**Deferred until core functionality is solid.**

2. **Pattern export/sharing system**
   - Copy pattern JSON to clipboard
   - Import patterns from clipboard or URL
   - Pattern library browser (community patterns)
   - **Complexity**: Medium-High - depends on implementation approach

---

### Key Questions to Answer

**In-game Editor**:
- Replace JSON or supplement? (supplement - JSON should remain primary)
- Pattern size limits? (match current system - reasonable bounds like 10x10x100)
- Block search/filter? (essential for usability)
- How to edit 3D patterns? (slice-by-slice editor with navigation, copy/paste slices)
- Preview while editing? (yes, essential for 3D pattern visualization)

---

# Known Bugs

## Active
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
- ~~When given an asymmetrical hoizontal pattern the system still thinks the player is in the center~~ - Fixed by returning player x and y from `FindPlayerPosition()` (v0.4.5)

## Known Limitations (Acceptable - Documented)
- **Sprinting downhill**: Skips 20-30% of descending stairs when sprinting (works perfectly at walking speed)
  - **Why**: Tick rate (50ms) can't catch all Y-changes at sprint speed (~7-8 blocks/sec)
  - **Mitigation**: Walk (don't sprint) when descending for best results
  - **Status**: Documented in README, won't fix (over-engineering for edge case)
