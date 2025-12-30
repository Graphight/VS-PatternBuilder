using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PatternBuilder.Pattern;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PatternBuilder.GUI;

public class PatternEditorDialog : GuiDialog
{
    private readonly ICoreClientAPI capi;
    private readonly Action<int> onPatternSaved;

    private int targetSlot;
    private string patternName = "";
    private string patternDescription = "";
    private string patternMode = "adaptive";
    private int gridWidth = 5;
    private int gridHeight = 5;

    private List<char[,]> listSlices;
    private int indexSliceCurrent = 0;
    private char[,] sliceClipboard = null;
    private char charBlockSelected = 'B';
    private string codeBlockSelected = "game:cobblestone-granite";

    private Dictionary<char, string> mapBlocks;
    private List<(char character, string blockCode, string displayName)> listBlocksAvailable;
    private List<(char character, string blockCode, string displayName)> listBlocksFiltered;
    private string searchFilter = "";

    public override string ToggleKeyCombinationCode => "patterneditor";

    public PatternEditorDialog(ICoreClientAPI capi, Action<int> onPatternSaved) : base(capi)
    {
        this.capi = capi;
        this.onPatternSaved = onPatternSaved;

        InitializeAvailableBlocks();
        InitializeEmptyGrid();
    }

    public void OpenForNewPattern(int slot)
    {
        targetSlot = slot;
        patternName = $"New Pattern";
        patternDescription = "";
        patternMode = "adaptive";
        gridWidth = 5;
        gridHeight = 5;
        searchFilter = "";

        InitializeEmptyGrid();
        FilterBlocks();
        TryOpen();
    }

    public void OpenForExistingPattern(int slot, PatternDefinition pattern)
    {
        targetSlot = slot;
        patternName = pattern.Name;
        patternDescription = pattern.Description ?? "";
        patternMode = pattern.Mode ?? "adaptive";
        gridWidth = pattern.Width;
        gridHeight = pattern.Height;
        searchFilter = "";

        LoadPatternIntoGrid(pattern);
        FilterBlocks();
        TryOpen();
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        SetupDialog();
    }

    private void InitializeAvailableBlocks()
    {
        listBlocksAvailable = new List<(char, string, string)>();

        listBlocksAvailable.Add(('_', "air", "Air (Empty)"));
        listBlocksAvailable.Add(('P', "player", "Player Marker"));

        var processedBlocks = new HashSet<string>();

        foreach (var block in capi.World.Blocks)
        {
            if (block == null || block.Code == null) continue;

            string blockCode = block.Code.ToString();

            if (processedBlocks.Contains(blockCode)) continue;

            if (blockCode == "air" || blockCode == "game:air") continue;

            if (block.BlockMaterial == Vintagestory.API.Common.EnumBlockMaterial.Liquid) continue;

            if (blockCode.Contains("water") || blockCode.Contains("lava")) continue;

            if (block.Id == 0) continue;

            string displayName;
            try
            {
                displayName = block.GetHeldItemName(new Vintagestory.API.Common.ItemStack(block));
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = blockCode;
                }
            }
            catch
            {
                displayName = blockCode;
            }

            listBlocksAvailable.Add(('\0', blockCode, displayName));
            processedBlocks.Add(blockCode);
        }

        listBlocksAvailable = listBlocksAvailable
            .OrderBy(b => b.displayName)
            .ToList();

        listBlocksFiltered = new List<(char, string, string)>(listBlocksAvailable);

        mapBlocks = new Dictionary<char, string>();
        mapBlocks['_'] = "air";
        mapBlocks['P'] = "player";

        capi.Logger.Notification($"PatternEditor: Loaded {listBlocksAvailable.Count} blocks from registry");
    }

    private void InitializeEmptyGrid()
    {
        listSlices = new List<char[,]>();
        var grid = new char[gridHeight, gridWidth];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                grid[y, x] = '_';
            }
        }

        if (gridWidth >= 1 && gridHeight >= 1)
        {
            grid[0, gridWidth / 2] = 'P';
        }

        listSlices.Add(grid);
        indexSliceCurrent = 0;
    }

    private char GetNextAvailableCharacter()
    {
        var usedChars = new HashSet<char>(mapBlocks.Keys);

        for (char c = 'A'; c <= 'Z'; c++)
        {
            if (!usedChars.Contains(c))
                return c;
        }

        for (char c = 'a'; c <= 'z'; c++)
        {
            if (!usedChars.Contains(c))
                return c;
        }

        for (char c = '0'; c <= '9'; c++)
        {
            if (!usedChars.Contains(c))
                return c;
        }

        return '?';
    }

    private void FilterBlocks()
    {
        if (string.IsNullOrWhiteSpace(searchFilter))
        {
            listBlocksFiltered = new List<(char, string, string)>
            {
                ('_', "air", "Air (Empty)"),
                ('P', "player", "Player Marker")
            };
        }
        else
        {
            string filter = searchFilter.ToLowerInvariant();
            listBlocksFiltered = listBlocksAvailable
                .Where(b => b.displayName.ToLowerInvariant().Contains(filter) ||
                           b.blockCode.ToLowerInvariant().Contains(filter))
                .ToList();
        }
    }

    private void LoadPatternIntoGrid(PatternDefinition pattern)
    {
        listSlices = new List<char[,]>();
        mapBlocks = new Dictionary<char, string>(pattern.Blocks);

        capi.Logger.Notification($"PatternEditor: Loading pattern with {pattern.Slices.Length} slice(s). Width={gridWidth}, Height={gridHeight}");

        for (int sliceIdx = 0; sliceIdx < pattern.Slices.Length; sliceIdx++)
        {
            var grid = new char[gridHeight, gridWidth];
            string sliceString = pattern.Slices[sliceIdx];

            capi.Logger.Notification($"PatternEditor: Loading slice {sliceIdx}: {sliceString}");

            string[] layers = sliceString.Split(',');
            if (layers.Length == gridHeight)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    string layer = layers[y];
                    if (layer.Length == gridWidth)
                    {
                        for (int x = 0; x < gridWidth; x++)
                        {
                            grid[y, x] = layer[x];
                        }
                    }
                    else
                    {
                        capi.Logger.Warning($"PatternEditor: Slice {sliceIdx}, Layer {y} width mismatch: expected {gridWidth}, got {layer.Length}");
                        for (int x = 0; x < gridWidth && x < layer.Length; x++)
                        {
                            grid[y, x] = layer[x];
                        }
                    }
                }
            }
            else
            {
                capi.Logger.Warning($"PatternEditor: Slice {sliceIdx} height mismatch: expected {gridHeight}, got {layers.Length}");
                for (int y = 0; y < gridHeight && y < layers.Length; y++)
                {
                    string layer = layers[y];
                    for (int x = 0; x < gridWidth && x < layer.Length; x++)
                    {
                        grid[y, x] = layer[x];
                    }
                }
            }

            listSlices.Add(grid);
        }

        if (listSlices.Count == 0)
        {
            capi.Logger.Warning($"PatternEditor: No valid listSlices loaded. Initializing empty grid.");
            InitializeEmptyGrid();
        }
        else
        {
            indexSliceCurrent = 0;
            capi.Logger.Notification($"PatternEditor: Loaded {listSlices.Count} slice(s). First slice, first row: {new string(Enumerable.Range(0, gridWidth).Select(x => listSlices[0][0, x]).ToArray())}");
        }
    }

    private void SetupDialog()
    {
        ElementBounds boundsDialog = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        ElementBounds boundsBg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        boundsBg.BothSizing = ElementSizing.FitToChildren;

        double currentY = 30;

        ElementBounds boundsMetaNameLabel = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds boundsMetaName = ElementBounds.Fixed(85, currentY, 300, 30);
        currentY += 40;

        ElementBounds boundsMetaDescLabel = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds boundsMetaDesc = ElementBounds.Fixed(85, currentY, 300, 30);
        currentY += 40;

        ElementBounds boundsMetaModeLabel = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds boundsMetaMode = ElementBounds.Fixed(85, currentY, 150, 30);
        currentY += 40;

        ElementBounds boundsMetaWidthLabel = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds boundsMetaWidth = ElementBounds.Fixed(85, currentY, 60, 30);
        ElementBounds boundsMetaHeightLabel = ElementBounds.Fixed(155, currentY, 80, 25);
        ElementBounds boundsMetaHeight = ElementBounds.Fixed(240, currentY, 60, 30);
        ElementBounds boundsMetaResize = ElementBounds.Fixed(310, currentY, 100, 30);
        currentY += 50;

        ElementBounds boundsSlicePrev = ElementBounds.Fixed(0, currentY, 100, 30);
        ElementBounds boundsSliceCounter = ElementBounds.Fixed(110, currentY + 5, 240, 25);
        ElementBounds boundsSliceNext = ElementBounds.Fixed(360, currentY, 100, 30);
        currentY += 40;

        ElementBounds boundsSliceAdd = ElementBounds.Fixed(0, currentY, 120, 30);
        ElementBounds boundsSliceDelete = ElementBounds.Fixed(130, currentY, 120, 30);
        ElementBounds boundsSliceCopy = ElementBounds.Fixed(260, currentY, 120, 30);
        ElementBounds boundsSlicePaste = ElementBounds.Fixed(390, currentY, 120, 30);
        currentY += 40;

        double gridStartY = currentY;
        ElementBounds boundsGridContainer = ElementBounds.Fixed(0, currentY, 450, 400);

        ElementBounds boundsSearchLabel = ElementBounds.Fixed(460, currentY, 80, 25);
        ElementBounds boundsSearch = ElementBounds.Fixed(460, currentY + 5, 250, 30);
        double pickerStartY = currentY + 40;

        ElementBounds boundsPicker = ElementBounds.Fixed(460, pickerStartY, 250, 360);
        ElementBounds boundsPickerClip = boundsPicker.ForkBoundingParent();
        ElementBounds boundsPickerInset = boundsPicker.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
        ElementBounds boundsPickerScrollbar = boundsPickerInset.CopyOffsetedSibling(boundsPickerInset.fixedWidth + 7, 0, 0, 0).WithFixedWidth(20);

        currentY += 410;

        ElementBounds boundsSave = ElementBounds.Fixed(460, currentY, 120, 30);
        ElementBounds boundsCancel = ElementBounds.Fixed(590, currentY, 120, 30);

        string[] modeValues = new string[] { "adaptive", "carve" };
        string[] modeNames = new string[] { "Adaptive", "Carve" };

        var composer = capi.Gui.CreateCompo("patterneditor", boundsDialog)
            .AddShadedDialogBG(boundsBg)
            .AddDialogTitleBar($"Pattern Editor - Slot {targetSlot}", OnClose)
            .BeginChildElements(boundsBg)
                .AddStaticText("Name:", CairoFont.WhiteSmallText(), boundsMetaNameLabel)
                .AddTextInput(boundsMetaName, OnNameChanged, CairoFont.WhiteDetailText(), "name-input")
                .AddStaticText("Description:", CairoFont.WhiteSmallText(), boundsMetaDescLabel)
                .AddTextInput(boundsMetaDesc, OnDescriptionChanged, CairoFont.WhiteDetailText(), "description-input")
                .AddStaticText("Mode:", CairoFont.WhiteSmallText(), boundsMetaModeLabel)
                .AddDropDown(modeValues, modeNames, Array.IndexOf(modeValues, patternMode), OnModeChanged, boundsMetaMode, "mode-dropdown")
                .AddStaticText("Width:", CairoFont.WhiteSmallText(), boundsMetaWidthLabel)
                .AddTextInput(boundsMetaWidth, OnWidthChanged, CairoFont.WhiteDetailText(), "width-input")
                .AddStaticText("Height:", CairoFont.WhiteSmallText(), boundsMetaHeightLabel)
                .AddTextInput(boundsMetaHeight, OnHeightChanged, CairoFont.WhiteDetailText(), "height-input")
                .AddSmallButton("Resize Grid", OnResizeGrid, boundsMetaResize)
                .AddSmallButton("< Prev Slice", OnPreviousSlice, boundsSlicePrev)
                .AddStaticText($"Slice {indexSliceCurrent + 1} of {listSlices.Count}", CairoFont.WhiteSmallText(), boundsSliceCounter, "slice-counter")
                .AddSmallButton("Next Slice >", OnNextSlice, boundsSliceNext)
                .AddSmallButton("Add Slice", OnAddSlice, boundsSliceAdd)
                .AddSmallButton("Delete Slice", OnDeleteSlice, boundsSliceDelete)
                .AddSmallButton("Copy Slice", OnCopySlice, boundsSliceCopy)
                .AddSmallButton("Paste Slice", OnPasteSlice, boundsSlicePaste)
                .AddStaticText("Type to search:", CairoFont.WhiteSmallText(), boundsSearchLabel)
                .AddTextInput(boundsSearch, OnSearchChanged, CairoFont.WhiteDetailText(), "search-input");

        double cellSize = 28;
        double gridSpacing = 2;
        double totalGridWidth = gridWidth * (cellSize + gridSpacing);
        double totalGridHeight = gridHeight * (cellSize + gridSpacing);

        var currentGrid = listSlices[indexSliceCurrent];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                double cellX = x * (cellSize + gridSpacing);
                double cellY = gridStartY + ((gridHeight - 1 - y) * (cellSize + gridSpacing));

                ElementBounds boundsCell = ElementBounds.Fixed(cellX, cellY, cellSize, cellSize);

                int capturedX = x;
                int capturedY = y;
                char cellChar = currentGrid[y, x];
                string cellText = cellChar.ToString();

                composer.AddButton(cellText, () => OnGridCellClicked(capturedX, capturedY), boundsCell, CairoFont.WhiteSmallText(), EnumButtonStyle.Small, $"grid-cell-{x}-{y}");
            }
        }

        composer.AddInset(boundsPickerInset, 3)
                .BeginClip(boundsPickerClip);

        double blockPickerY = 0;
        int blockIndex = 0;
        foreach (var (character, blockCode, displayName) in listBlocksFiltered)
        {
            ElementBounds boundsBlock = ElementBounds.Fixed(0, blockPickerY, 250, 25);

            char displayChar = character != '\0' ? character :
                (mapBlocks.ContainsValue(blockCode) ? mapBlocks.FirstOrDefault(kvp => kvp.Value == blockCode).Key : ' ');

            string buttonText = displayChar != ' ' ? $"{displayChar} - {displayName}" : displayName;
            char capturedChar = character;
            string capturedCode = blockCode;

            composer.AddButton(buttonText, () => OnBlockSelected(capturedChar, capturedCode), boundsBlock, CairoFont.WhiteDetailText(), EnumButtonStyle.MainMenu, $"block-{blockIndex}");
            blockPickerY += 27;
            blockIndex++;
        }

        composer.EndClip()
            .AddVerticalScrollbar(OnBlockPickerScroll, boundsPickerScrollbar, "block-picker-scrollbar")
            .AddSmallButton("Save Pattern", OnSavePattern, boundsSave)
            .AddSmallButton("Cancel", OnCloseButton, boundsCancel)
            .EndChildElements()
            .Compose();

        SingleComposer = composer;

        SingleComposer.GetScrollbar("block-picker-scrollbar").SetHeights(
            (float)boundsPicker.fixedHeight,
            (float)blockPickerY
        );

        SingleComposer.GetTextInput("name-input").SetValue(patternName);
        SingleComposer.GetTextInput("description-input").SetValue(patternDescription);
        SingleComposer.GetTextInput("width-input").SetValue(gridWidth.ToString());
        SingleComposer.GetTextInput("height-input").SetValue(gridHeight.ToString());
        SingleComposer.GetTextInput("search-input").SetValue(searchFilter);
    }

    private void OnNameChanged(string value)
    {
        patternName = value;
    }

    private void OnDescriptionChanged(string value)
    {
        patternDescription = value;
    }

    private void OnSearchChanged(string value)
    {
        searchFilter = value;
        FilterBlocks();
        RefreshGrid();
    }

    private void OnModeChanged(string value, bool selected)
    {
        if (selected)
        {
            patternMode = value;
        }
    }

    private void OnWidthChanged(string value)
    {
        if (int.TryParse(value, out int width) && width > 0 && width <= 15)
        {
            gridWidth = width;
        }
    }

    private void OnHeightChanged(string value)
    {
        if (int.TryParse(value, out int height) && height > 0 && height <= 15)
        {
            gridHeight = height;
        }
    }

    private bool OnResizeGrid()
    {
        if (gridWidth < 1 || gridWidth > 15 || gridHeight < 1 || gridHeight > 15)
        {
            capi.ShowChatMessage($"Grid dimensions must be between 1x1 and 15x15");
            return true;
        }

        InitializeEmptyGrid();
        RefreshGrid();
        return true;
    }

    private bool OnPreviousSlice()
    {
        if (indexSliceCurrent > 0)
        {
            indexSliceCurrent--;
            capi.Logger.Notification($"PatternEditor: Navigated to slice {indexSliceCurrent}");
            RefreshGrid();
        }
        else
        {
            capi.ShowChatMessage("Already at first slice");
        }
        return true;
    }

    private bool OnNextSlice()
    {
        if (indexSliceCurrent < listSlices.Count - 1)
        {
            indexSliceCurrent++;
            capi.Logger.Notification($"PatternEditor: Navigated to slice {indexSliceCurrent}");
            RefreshGrid();
        }
        else
        {
            capi.ShowChatMessage("Already at last slice");
        }
        return true;
    }

    private bool OnAddSlice()
    {
        var currentGrid = listSlices[indexSliceCurrent];
        var newGrid = new char[gridHeight, gridWidth];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                newGrid[y, x] = currentGrid[y, x];
            }
        }

        listSlices.Insert(indexSliceCurrent + 1, newGrid);
        indexSliceCurrent++;

        capi.Logger.Notification($"PatternEditor: Added new slice at index {indexSliceCurrent}. Total listSlices: {listSlices.Count}");
        capi.ShowChatMessage($"Added slice {indexSliceCurrent + 1} (copy of previous slice)");
        RefreshGrid();
        return true;
    }

    private bool OnDeleteSlice()
    {
        if (listSlices.Count <= 1)
        {
            capi.ShowChatMessage("Cannot delete the last remaining slice");
            return true;
        }

        int deletedIndex = indexSliceCurrent;
        listSlices.RemoveAt(indexSliceCurrent);

        if (indexSliceCurrent >= listSlices.Count)
        {
            indexSliceCurrent = listSlices.Count - 1;
        }

        capi.Logger.Notification($"PatternEditor: Deleted slice at index {deletedIndex}. Total listSlices: {listSlices.Count}. Now at slice {indexSliceCurrent}");
        capi.ShowChatMessage($"Deleted slice {deletedIndex + 1}. Now at slice {indexSliceCurrent + 1}");
        RefreshGrid();
        return true;
    }

    private bool OnCopySlice()
    {
        var currentGrid = listSlices[indexSliceCurrent];
        sliceClipboard = new char[gridHeight, gridWidth];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                sliceClipboard[y, x] = currentGrid[y, x];
            }
        }

        capi.Logger.Notification($"PatternEditor: Copied slice {indexSliceCurrent} to clipboard (dimensions: {gridWidth}x{gridHeight})");
        capi.ShowChatMessage($"Copied slice {indexSliceCurrent + 1} to clipboard");
        return true;
    }

    private bool OnPasteSlice()
    {
        if (sliceClipboard == null)
        {
            capi.ShowChatMessage("Clipboard is empty. Copy a slice first.");
            return true;
        }

        int clipboardWidth = sliceClipboard.GetLength(1);
        int clipboardHeight = sliceClipboard.GetLength(0);

        if (clipboardWidth != gridWidth || clipboardHeight != gridHeight)
        {
            capi.ShowChatMessage($"Cannot paste: clipboard dimensions ({clipboardWidth}x{clipboardHeight}) don't match current grid ({gridWidth}x{gridHeight})");
            return true;
        }

        var currentGrid = listSlices[indexSliceCurrent];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                currentGrid[y, x] = sliceClipboard[y, x];
            }
        }

        capi.Logger.Notification($"PatternEditor: Pasted clipboard to slice {indexSliceCurrent}");
        capi.ShowChatMessage($"Pasted clipboard to slice {indexSliceCurrent + 1}");
        RefreshGrid();
        return true;
    }

    private bool OnBlockSelected(char character, string blockCode)
    {
        if (character == '\0' || !mapBlocks.ContainsKey(character))
        {
            char existingChar = mapBlocks.FirstOrDefault(kvp => kvp.Value == blockCode).Key;
            if (existingChar != '\0')
            {
                charBlockSelected = existingChar;
            }
            else
            {
                charBlockSelected = GetNextAvailableCharacter();
                mapBlocks[charBlockSelected] = blockCode;
            }
        }
        else
        {
            charBlockSelected = character;
        }

        codeBlockSelected = blockCode;
        capi.ShowChatMessage($"Selected: {charBlockSelected} - {blockCode}");
        return true;
    }

    private bool OnGridCellClicked(int x, int y)
    {
        capi.Logger.Notification($"Grid cell clicked: ({x}, {y}), painting '{charBlockSelected}' on slice {indexSliceCurrent}");

        listSlices[indexSliceCurrent][y, x] = charBlockSelected;

        if (!mapBlocks.ContainsKey(charBlockSelected) && charBlockSelected != '_')
        {
            mapBlocks[charBlockSelected] = codeBlockSelected;
        }

        RefreshGrid();

        return true;
    }

    private void RefreshGrid()
    {
        capi.Event.EnqueueMainThreadTask(() => {
            SetupDialog();
        }, "pattern-editor-refresh");
    }

    private void OnBlockPickerScroll(float value)
    {
        var composer = SingleComposer;
        if (composer == null) return;

        for (int i = 0; i < listBlocksFiltered.Count; i++)
        {
            var button = composer.GetButton($"block-{i}");
            if (button != null)
            {
                double originalY = i * 27;
                button.Bounds.fixedY = originalY - value;
                button.Bounds.CalcWorldBounds();
            }
        }
    }

    private bool OnSavePattern()
    {
        capi.Logger.Notification("PatternEditor: OnSavePattern called");
        capi.ShowChatMessage("Save button clicked - starting validation...");

        if (string.IsNullOrWhiteSpace(patternName))
        {
            capi.ShowChatMessage("Pattern name cannot be empty");
            return true;
        }

        if (gridWidth < 1 || gridHeight < 1)
        {
            capi.ShowChatMessage("Grid dimensions must be at least 1x1");
            return true;
        }

        bool hasPlayerMarker = false;
        var currentGrid = listSlices[indexSliceCurrent];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (currentGrid[y, x] == 'P')
                {
                    hasPlayerMarker = true;
                    break;
                }
            }
        }

        if (!hasPlayerMarker)
        {
            capi.ShowChatMessage("Pattern must have a 'P' (player) marker. Select 'P' from block picker and place it in the grid.");
            return true;
        }

        string[] sliceStrings = BuildSliceStrings();
        capi.Logger.Notification($"PatternEditor: Built {sliceStrings.Length} slice string(s)");

        var usedChars = new HashSet<char>();
        foreach (var grid in listSlices)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    char c = grid[y, x];
                    if (c != '_')
                    {
                        usedChars.Add(c);
                    }
                }
            }
        }

        var cleanedBlocks = new Dictionary<char, string>();
        foreach (char c in usedChars)
        {
            if (mapBlocks.ContainsKey(c))
            {
                string blockCode = mapBlocks[c];
                if (blockCode != "air")
                {
                    cleanedBlocks[c] = blockCode;
                }
            }
        }
        capi.Logger.Notification($"PatternEditor: Used chars: {string.Join(", ", usedChars)}");
        capi.Logger.Notification($"PatternEditor: Cleaned blocks: {string.Join(", ", cleanedBlocks.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

        var pattern = new PatternDefinition
        {
            Name = patternName,
            Description = string.IsNullOrWhiteSpace(patternDescription) ? null : patternDescription,
            Slices = sliceStrings,
            Width = gridWidth,
            Height = gridHeight,
            Mode = patternMode,
            Blocks = cleanedBlocks
        };

        capi.Logger.Notification("PatternEditor: Calling validation...");
        var errors = pattern.GetValidationErrors(capi);
        capi.Logger.Notification($"PatternEditor: Validation returned {errors.Count} errors");

        if (errors.Count > 0)
        {
            string errorMsg = string.Join(", ", errors);
            capi.Logger.Warning($"PatternEditor: Validation errors: {errorMsg}");
            capi.ShowChatMessage($"Pattern validation failed: {errorMsg}");
            return true;
        }

        capi.Logger.Notification("PatternEditor: Validation passed, proceeding to save...");

        string configPath = Path.Combine(capi.GetOrCreateDataPath("ModConfig"), "patternbuilder", "patterns");

        if (!Directory.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
        }

        var existingFiles = Directory.GetFiles(configPath, $"slot{targetSlot}_*.json");
        foreach (var oldFile in existingFiles)
        {
            try
            {
                File.Delete(oldFile);
                capi.Logger.Notification($"PatternEditor: Deleted old file {Path.GetFileName(oldFile)}");
            }
            catch (Exception ex)
            {
                capi.Logger.Warning($"PatternEditor: Failed to delete old file {oldFile}: {ex.Message}");
            }
        }

        string sanitizedName = SanitizeFileName(patternName);
        string fileName = $"slot{targetSlot}_{sanitizedName}.json";
        string filePath = Path.Combine(configPath, fileName);

        try
        {
            string json = JsonConvert.SerializeObject(pattern, Formatting.Indented);
            File.WriteAllText(filePath, json);

            capi.ShowChatMessage($"Pattern saved to slot {targetSlot}: {fileName}");
            capi.Logger.Notification($"PatternEditor: Saved pattern to {filePath}");
            onPatternSaved?.Invoke(targetSlot);
            TryClose();
        }
        catch (Exception ex)
        {
            capi.ShowChatMessage($"Failed to save pattern: {ex.Message}");
            capi.Logger.Error($"PatternEditor: Failed to save pattern: {ex}");
        }

        return true;
    }

    private string[] BuildSliceStrings()
    {
        var sliceStrings = new List<string>();

        foreach (var grid in listSlices)
        {
            var rows = new List<string>();

            for (int y = 0; y < gridHeight; y++)
            {
                var rowChars = new char[gridWidth];
                for (int x = 0; x < gridWidth; x++)
                {
                    rowChars[x] = grid[y, x];
                }
                rows.Add(new string(rowChars));
            }

            sliceStrings.Add(string.Join(",", rows));
        }

        return sliceStrings.ToArray();
    }

    private string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace(" ", "_").ToLowerInvariant();
    }

    private bool OnCloseButton()
    {
        TryClose();
        return true;
    }

    private void OnClose()
    {
        TryClose();
    }
}
