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

    private List<char[,]> slices;
    private int currentSliceIndex = 0;
    private char selectedBlockChar = 'B';
    private string selectedBlockCode = "game:cobblestone-granite";

    private Dictionary<char, string> blockMappings;
    private List<(char character, string blockCode, string displayName)> availableBlocks;

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

        InitializeEmptyGrid();
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

        LoadPatternIntoGrid(pattern);
        TryOpen();
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        SetupDialog();
    }

    private void InitializeAvailableBlocks()
    {
        availableBlocks = new List<(char, string, string)>
        {
            ('_', "air", "Air (Empty)"),
            ('B', "game:cobblestone-granite", "Cobblestone"),
            ('C', "game:stonebricks-granite", "Stone Bricks"),
            ('D', "game:soil-medium-normal", "Soil"),
            ('E', "game:gravel-granite", "Gravel"),
            ('F', "game:log-placed-oak-ud", "Oak Log"),
            ('G', "game:planks-oak", "Oak Planks"),
            ('H', "game:claybricks-fire", "Clay Bricks"),
            ('I', "game:glass-plain", "Glass"),
            ('J', "game:sand-granite", "Sand"),
            ('K', "game:stonebrickstairs-granite-north|up|f", "Stone Stairs"),
            ('L', "game:torch-ground-lit-north", "Torch"),
            ('M', "game:lantern-iron-on", "Lantern"),
            ('N', "game:woodenfence-oak-empty-free", "Fence"),
            ('O', "game:paperlantern-on", "Paper Lantern"),
            ('P', "player", "Player"),
        };

        blockMappings = new Dictionary<char, string>();
        foreach (var (character, blockCode, _) in availableBlocks)
        {
            blockMappings[character] = blockCode;
        }
    }

    private void InitializeEmptyGrid()
    {
        slices = new List<char[,]>();
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

        slices.Add(grid);
        currentSliceIndex = 0;
    }

    private void LoadPatternIntoGrid(PatternDefinition pattern)
    {
        slices = new List<char[,]>();
        blockMappings = new Dictionary<char, string>(pattern.Blocks);

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

            slices.Add(grid);
        }

        if (slices.Count == 0)
        {
            capi.Logger.Warning($"PatternEditor: No valid slices loaded. Initializing empty grid.");
            InitializeEmptyGrid();
        }
        else
        {
            currentSliceIndex = 0;
            capi.Logger.Notification($"PatternEditor: Loaded {slices.Count} slice(s). First slice, first row: {new string(Enumerable.Range(0, gridWidth).Select(x => slices[0][0, x]).ToArray())}");
        }
    }

    private void SetupDialog()
    {
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        double currentY = 30;

        ElementBounds nameLabelBounds = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds nameInputBounds = ElementBounds.Fixed(85, currentY, 300, 30);
        currentY += 40;

        ElementBounds descLabelBounds = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds descInputBounds = ElementBounds.Fixed(85, currentY, 300, 30);
        currentY += 40;

        ElementBounds modeLabelBounds = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds modeDropdownBounds = ElementBounds.Fixed(85, currentY, 150, 30);
        currentY += 40;

        ElementBounds widthLabelBounds = ElementBounds.Fixed(0, currentY, 80, 25);
        ElementBounds widthInputBounds = ElementBounds.Fixed(85, currentY, 60, 30);
        ElementBounds heightLabelBounds = ElementBounds.Fixed(155, currentY, 80, 25);
        ElementBounds heightInputBounds = ElementBounds.Fixed(240, currentY, 60, 30);
        ElementBounds resizeButtonBounds = ElementBounds.Fixed(310, currentY, 100, 30);
        currentY += 50;

        ElementBounds prevSliceBounds = ElementBounds.Fixed(0, currentY, 100, 30);
        ElementBounds sliceCounterBounds = ElementBounds.Fixed(110, currentY + 5, 240, 25);
        ElementBounds nextSliceBounds = ElementBounds.Fixed(360, currentY, 100, 30);
        currentY += 40;

        double gridStartY = currentY;
        ElementBounds gridContainerBounds = ElementBounds.Fixed(0, currentY, 450, 400);
        ElementBounds blockPickerBounds = ElementBounds.Fixed(460, currentY, 250, 400);
        ElementBounds blockPickerClipBounds = blockPickerBounds.ForkBoundingParent();
        ElementBounds blockPickerInsetBounds = blockPickerBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
        ElementBounds blockPickerScrollbarBounds = blockPickerInsetBounds.CopyOffsetedSibling(blockPickerInsetBounds.fixedWidth + 7, 0, 0, 0).WithFixedWidth(20);

        currentY += 410;

        ElementBounds saveButtonBounds = ElementBounds.Fixed(460, currentY, 120, 30);
        ElementBounds cancelButtonBounds = ElementBounds.Fixed(590, currentY, 120, 30);

        string[] modeValues = new string[] { "adaptive", "carve" };
        string[] modeNames = new string[] { "Adaptive", "Carve" };

        var composer = capi.Gui.CreateCompo("patterneditor", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar($"Pattern Editor - Slot {targetSlot}", OnClose)
            .BeginChildElements(bgBounds)
                .AddStaticText("Name:", CairoFont.WhiteSmallText(), nameLabelBounds)
                .AddTextInput(nameInputBounds, OnNameChanged, CairoFont.WhiteDetailText(), "name-input")
                .AddStaticText("Description:", CairoFont.WhiteSmallText(), descLabelBounds)
                .AddTextInput(descInputBounds, OnDescriptionChanged, CairoFont.WhiteDetailText(), "description-input")
                .AddStaticText("Mode:", CairoFont.WhiteSmallText(), modeLabelBounds)
                .AddDropDown(modeValues, modeNames, Array.IndexOf(modeValues, patternMode), OnModeChanged, modeDropdownBounds, "mode-dropdown")
                .AddStaticText("Width:", CairoFont.WhiteSmallText(), widthLabelBounds)
                .AddTextInput(widthInputBounds, OnWidthChanged, CairoFont.WhiteDetailText(), "width-input")
                .AddStaticText("Height:", CairoFont.WhiteSmallText(), heightLabelBounds)
                .AddTextInput(heightInputBounds, OnHeightChanged, CairoFont.WhiteDetailText(), "height-input")
                .AddSmallButton("Resize Grid", OnResizeGrid, resizeButtonBounds)
                .AddSmallButton("< Prev Slice", OnPreviousSlice, prevSliceBounds)
                .AddStaticText($"Slice {currentSliceIndex + 1} of {slices.Count}", CairoFont.WhiteSmallText(), sliceCounterBounds, "slice-counter")
                .AddSmallButton("Next Slice >", OnNextSlice, nextSliceBounds);

        double cellSize = 28;
        double gridSpacing = 2;
        double totalGridWidth = gridWidth * (cellSize + gridSpacing);
        double totalGridHeight = gridHeight * (cellSize + gridSpacing);

        var currentGrid = slices[currentSliceIndex];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                double cellX = x * (cellSize + gridSpacing);
                double cellY = gridStartY + ((gridHeight - 1 - y) * (cellSize + gridSpacing));

                ElementBounds cellBounds = ElementBounds.Fixed(cellX, cellY, cellSize, cellSize);

                int capturedX = x;
                int capturedY = y;
                char cellChar = currentGrid[y, x];
                string cellText = cellChar.ToString();

                composer.AddButton(cellText, () => OnGridCellClicked(capturedX, capturedY), cellBounds, CairoFont.WhiteSmallText(), EnumButtonStyle.Small, $"grid-cell-{x}-{y}");
            }
        }

        composer.AddInset(blockPickerInsetBounds, 3)
                .BeginClip(blockPickerClipBounds);

        double blockPickerY = 0;
        foreach (var (character, blockCode, displayName) in availableBlocks)
        {
            ElementBounds blockBounds = ElementBounds.Fixed(0, blockPickerY, 250, 25);
            string buttonText = $"{character} - {displayName}";
            char capturedChar = character;
            string capturedCode = blockCode;

            composer.AddButton(buttonText, () => OnBlockSelected(capturedChar, capturedCode), blockBounds, CairoFont.WhiteDetailText(), EnumButtonStyle.MainMenu, $"block-{character}");
            blockPickerY += 27;
        }

        composer.EndClip()
            .AddVerticalScrollbar(OnBlockPickerScroll, blockPickerScrollbarBounds, "block-picker-scrollbar")
            .AddSmallButton("Save Pattern", OnSavePattern, saveButtonBounds)
            .AddSmallButton("Cancel", OnCloseButton, cancelButtonBounds)
            .EndChildElements()
            .Compose();

        SingleComposer = composer;

        SingleComposer.GetScrollbar("block-picker-scrollbar").SetHeights(
            (float)blockPickerBounds.fixedHeight,
            (float)blockPickerY
        );

        SingleComposer.GetTextInput("name-input").SetValue(patternName);
        SingleComposer.GetTextInput("description-input").SetValue(patternDescription);
        SingleComposer.GetTextInput("width-input").SetValue(gridWidth.ToString());
        SingleComposer.GetTextInput("height-input").SetValue(gridHeight.ToString());
    }

    private void OnNameChanged(string value)
    {
        patternName = value;
    }

    private void OnDescriptionChanged(string value)
    {
        patternDescription = value;
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
        if (currentSliceIndex > 0)
        {
            currentSliceIndex--;
            capi.Logger.Notification($"PatternEditor: Navigated to slice {currentSliceIndex}");
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
        if (currentSliceIndex < slices.Count - 1)
        {
            currentSliceIndex++;
            capi.Logger.Notification($"PatternEditor: Navigated to slice {currentSliceIndex}");
            RefreshGrid();
        }
        else
        {
            capi.ShowChatMessage("Already at last slice");
        }
        return true;
    }

    private bool OnBlockSelected(char character, string blockCode)
    {
        selectedBlockChar = character;
        selectedBlockCode = blockCode;
        capi.ShowChatMessage($"Selected: {character} - {blockCode}");
        return true;
    }

    private bool OnGridCellClicked(int x, int y)
    {
        capi.Logger.Notification($"Grid cell clicked: ({x}, {y}), painting '{selectedBlockChar}' on slice {currentSliceIndex}");

        slices[currentSliceIndex][y, x] = selectedBlockChar;

        if (!blockMappings.ContainsKey(selectedBlockChar) && selectedBlockChar != '_')
        {
            blockMappings[selectedBlockChar] = selectedBlockCode;
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

        foreach (var (character, _, _) in availableBlocks)
        {
            var button = composer.GetButton($"block-{character}");
            if (button != null)
            {
                double originalY = availableBlocks.FindIndex(b => b.character == character) * 27;
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
        var currentGrid = slices[currentSliceIndex];
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
        foreach (var grid in slices)
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
            if (blockMappings.ContainsKey(c))
            {
                string blockCode = blockMappings[c];
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

        foreach (var grid in slices)
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
