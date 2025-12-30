# Roadmap

## In Progress

### GUI Features (Sessions 1-5 Complete)
**Status**: Pattern browser and full 3D editor complete with advanced features
**Branch**: `FEAT-InGameEditor` (ready to merge)
**Documentation**: See `.claude/gui_feature_plan.md` and `.claude/session_notes/`

**Completed** (Sessions 1-5):
- **Session 1-2**: Pattern browser dialog with search/filter, validation indicators, info panel, scrollbars
  - Hotkey: Ctrl+Shift+Space
  - Command: `.pb browser`
- **Session 3**: 2D pattern editor with grid painting, block picker, save/load
  - Command: `.pb edit [slot]`
  - Features: Metadata inputs, 16 common blocks, resize grid, pattern validation
  - Saves to JSON, loads existing patterns
- **Session 4**: 3D pattern editor with slice management
  - Slice navigation (prev/next buttons, counter display)
  - Add/Delete slices (copy current, min 1 slice constraint)
  - Copy/Paste slices (dimension validation)
  - Save/load multi-slice patterns
  - VBA-style naming convention (51 variables renamed)
- **Session 5**: Enhanced block picker and import/export functionality
  - Enhanced block picker: Search 500+ game blocks with lazy loading
  - Dynamic character assignment (A-Z, a-z, 0-9)
  - Eyedropper tool: Sample blocks from grid (toggle "Pick" mode)
  - Import/Export JSON: Clipboard integration for pattern sharing
  - Critical bug fixes: Preserved block mappings, null safety in placement
  - **Note**: Text-based picker acknowledged as "jank" - visual picker deferred to next session

**Deferred from Session 5** (planned for future):
- Visual block picker (creative inventory style) - Next priority
- Block categories and favorites
- Real-time validation (save-time validation sufficient)
- Terrain following UI (architectural blocker)
- Undo/redo system (low priority)
- Delete pattern from browser (user suggestion)

---

## Planned Features

### Tier 3: Advanced Features (Nice to Have)
**Polish and power-user features that enhance but don't fundamentally change usage.**

1. **In-game pattern editor** (GUI-based pattern creation) - IN PROGRESS (see above)
   - Visual grid editor for pattern design
   - Block picker from game blocks
   - Live preview of pattern
   - Save/load to pattern slots
   - Import/export pattern JSON
   - Must support 3D patterns (slice editor)

2. **Unified GUI dashboard with tab navigation**
    - Single entry point dialog with tab system
    - Tabs: Home (quick controls), Browser, Editor, Settings
    - Home tab: On/Off/Toggle buttons, current pattern info, stats
    - Consolidates separate dialogs into cohesive interface
    - **Complexity**: Medium - requires dialog refactoring and tab UI implementation

3. **Enhanced block picker for editor GUI (creative inventory style)**
    - Visual block icons/textures instead of text labels
    - Category tabs (stone, wood, soil, functional, etc.)
    - Search/filter within categories
    - Support for all blocks including modded content
    - Reference: VS creative mode inventory system
    - Support our directional block feature
      - Edit a specific block in the pattern to have suffixes `|l|auto` etc
      - These will need a different character in the JSON block definition
    - **Complexity**: Medium - texture rendering, category organization

### Tier 4: Polish & Community Features
**Deferred until core functionality is solid.**

1. **Pattern export/sharing system**
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
- **Asymmetric pattern orientation**: Patterns don't rotate with player direction - "ABC" always places "A" north, "C" south regardless of which way player faces
- **Preview orientation bug**: Preview renderer doesn't respect player direction, always faces north (especially noticeable with tunnels)

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
