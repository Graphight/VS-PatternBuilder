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
    private bool blocksInitialized = false;
    private bool sampleMode = false;
    private string directionModifier = "";
    private static List<string> recentBlocks = new List<string>();
    private const int MaxRecentBlocks = 10;
    private string selectedCategory = "All";
    private static readonly string[] BlockCategories = { "All", "Stone", "Wood", "Soil", "Metal", "Glass", "Decor", "Func" };

    public override string ToggleKeyCombinationCode => "patterneditor";

    public PatternEditorDialog(ICoreClientAPI capi, Action<int> onPatternSaved) : base(capi)
    {
        this.capi = capi;
        this.onPatternSaved = onPatternSaved;

        listBlocksAvailable = new List<(char, string, string)>();
        listBlocksFiltered = new List<(char, string, string)>();
        mapBlocks = new Dictionary<char, string>();
        listSlices = new List<char[,]>();

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
        directionModifier = "";
        selectedCategory = "All";

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
        directionModifier = "";
        selectedCategory = "All";

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

        if (!mapBlocks.ContainsKey('_'))
            mapBlocks['_'] = "air";
        if (!mapBlocks.ContainsKey('P'))
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
        if (!blocksInitialized)
        {
            InitializeAvailableBlocks();
            blocksInitialized = true;
        }

        listBlocksFiltered = new List<(char, string, string)>
        {
            ('_', "air", "Air (Empty)"),
            ('P', "player", "Player Marker")
        };

        if (string.IsNullOrWhiteSpace(searchFilter))
        {
            if (selectedCategory != "All")
            {
                listBlocksFiltered.AddRange(listBlocksAvailable
                    .Where(b => b.blockCode != "air" && b.blockCode != "player" &&
                               GetBlockCategory(b.blockCode, b.displayName) == selectedCategory));
            }
        }
        else
        {
            string filter = searchFilter.ToLowerInvariant();
            var searchResults = listBlocksAvailable
                .Where(b => b.displayName.ToLowerInvariant().Contains(filter) ||
                           b.blockCode.ToLowerInvariant().Contains(filter));

            if (selectedCategory != "All")
            {
                searchResults = searchResults.Where(b =>
                    GetBlockCategory(b.blockCode, b.displayName) == selectedCategory);
            }

            listBlocksFiltered.AddRange(searchResults);
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

        ElementBounds boundsRecentLabel = ElementBounds.Fixed(460, currentY, 100, 20);
        double recentRowY = currentY + 22;

        ElementBounds boundsSearchLabel = ElementBounds.Fixed(460, currentY + 80, 100, 25);
        ElementBounds boundsSearch = ElementBounds.Fixed(460, currentY + 85, 210, 30);
        ElementBounds boundsSearchButton = ElementBounds.Fixed(675, currentY + 85, 65, 30);
        ElementBounds boundsSampleModeLabel = ElementBounds.Fixed(750, currentY + 80, 50, 25);
        ElementBounds boundsSampleMode = ElementBounds.Fixed(750, currentY + 85, 30, 30);

        ElementBounds boundsDirectionLabel = ElementBounds.Fixed(460, currentY + 120, 80, 25);
        ElementBounds boundsDirection = ElementBounds.Fixed(545, currentY + 120, 120, 30);

        double categoryY = currentY + 155;
        double pickerStartY = currentY + 185;

        ElementBounds boundsPicker = ElementBounds.Fixed(460, pickerStartY, 350, 215);
        ElementBounds boundsPickerClip = boundsPicker.ForkBoundingParent();
        ElementBounds boundsPickerInset = boundsPicker.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
        ElementBounds boundsPickerScrollbar = boundsPickerInset.CopyOffsetedSibling(boundsPickerInset.fixedWidth + 7, 0, 0, 0).WithFixedWidth(20);

        currentY += 410;

        ElementBounds boundsExport = ElementBounds.Fixed(460, currentY, 120, 30);
        ElementBounds boundsImport = ElementBounds.Fixed(590, currentY, 120, 30);
        ElementBounds boundsSave = ElementBounds.Fixed(460, currentY + 40, 120, 30);
        ElementBounds boundsCancel = ElementBounds.Fixed(590, currentY + 40, 120, 30);

        string[] modeValues = new string[] { "adaptive", "carve" };
        string[] modeNames = new string[] { "Adaptive", "Carve" };

        string[] directionValues = new string[] { "", "|f", "|b", "|l", "|r", "|up", "|down", "|auto" };
        string[] directionNames = new string[] { "None", "Forward", "Back", "Left", "Right", "Up", "Down", "Auto" };
        int directionIndex = Array.IndexOf(directionValues, directionModifier);
        if (directionIndex < 0) directionIndex = 0;

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
                .AddStaticText("Recent:", CairoFont.WhiteSmallText(), boundsRecentLabel);

        if (recentBlocks.Count == 0)
        {
            ElementBounds boundsNoRecent = ElementBounds.Fixed(460, recentRowY, 350, 22);
            composer.AddStaticText("(no recent blocks)", CairoFont.WhiteDetailText(), boundsNoRecent);
        }
        else
        {
            double recentX = 460;
            int col = 0;
            int row = 0;
            double buttonWidth = 170;
            double buttonHeight = 22;
            double spacing = 5;

            for (int i = 0; i < recentBlocks.Count && i < MaxRecentBlocks; i++)
            {
                string blockCode = recentBlocks[i];
                string displayName = GetBlockDisplayNameForCode(blockCode);
                if (displayName.Length > 18) displayName = displayName.Substring(0, 15) + "...";

                double btnX = recentX + col * (buttonWidth + spacing);
                double btnY = recentRowY + row * (buttonHeight + 3);
                ElementBounds boundsRecentBtn = ElementBounds.Fixed(btnX, btnY, buttonWidth, buttonHeight);

                string capturedCode = blockCode;
                composer.AddButton(displayName, () => OnRecentBlockClicked(capturedCode), boundsRecentBtn, CairoFont.WhiteDetailText(), EnumButtonStyle.Small, $"recent-{i}");

                col++;
                if (col >= 2)
                {
                    col = 0;
                    row++;
                }
            }
        }

        composer.AddStaticText("Pick:", CairoFont.WhiteSmallText(), boundsSampleModeLabel)
                .AddSwitch(OnSampleModeToggled, boundsSampleMode, "sample-mode-toggle")
                .AddStaticText("Direction:", CairoFont.WhiteSmallText(), boundsDirectionLabel)
                .AddDropDown(directionValues, directionNames, directionIndex, OnDirectionChanged, boundsDirection, "direction-dropdown")
                .AddStaticText("Type to search:", CairoFont.WhiteSmallText(), boundsSearchLabel)
                .AddTextInput(boundsSearch, OnSearchChanged, CairoFont.WhiteDetailText(), "search-input")
                .AddSmallButton("Search", OnSearchButtonClicked, boundsSearchButton);

        double catX = 460;
        double catBtnWidth = 42;
        for (int i = 0; i < BlockCategories.Length; i++)
        {
            string cat = BlockCategories[i];
            ElementBounds boundsCatBtn = ElementBounds.Fixed(catX + i * (catBtnWidth + 2), categoryY, catBtnWidth, 24);
            string capturedCat = cat;

            CairoFont catFont = cat == selectedCategory
                ? CairoFont.WhiteDetailText().WithWeight(Cairo.FontWeight.Bold)
                : CairoFont.WhiteDetailText();

            composer.AddButton(cat, () => OnCategoryClicked(capturedCat), boundsCatBtn, catFont, EnumButtonStyle.Small, $"cat-{cat}");
        }

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
            ElementBounds boundsBlock = ElementBounds.Fixed(0, blockPickerY, 350, 25);

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
            .AddSmallButton("Export JSON", OnExportJSON, boundsExport)
            .AddSmallButton("Import JSON", OnImportJSON, boundsImport)
            .AddSmallButton("Save Pattern", OnSavePattern, boundsSave)
            .AddSmallButton("Cancel", OnCloseButton, boundsCancel)
            .EndChildElements()
            .Compose();

        SingleComposer = composer;

        SingleComposer.GetScrollbar("block-picker-scrollbar").SetHeights(
            215f,
            (float)blockPickerY
        );

        SingleComposer.GetTextInput("name-input").SetValue(patternName);
        SingleComposer.GetTextInput("description-input").SetValue(patternDescription);
        SingleComposer.GetTextInput("width-input").SetValue(gridWidth.ToString());
        SingleComposer.GetTextInput("height-input").SetValue(gridHeight.ToString());
        SingleComposer.GetTextInput("search-input").SetValue(searchFilter);
        SingleComposer.GetSwitch("sample-mode-toggle").SetValue(sampleMode);
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
    }

    private bool OnSearchButtonClicked()
    {
        FilterBlocks();
        RefreshGrid();
        return true;
    }

    private void OnSampleModeToggled(bool on)
    {
        sampleMode = on;
        capi.ShowChatMessage(sampleMode ? "Sample mode ON (click grid to pick block)" : "Sample mode OFF (click grid to paint)");
    }

    private void OnModeChanged(string value, bool selected)
    {
        if (selected)
        {
            patternMode = value;
        }
    }

    private void OnDirectionChanged(string value, bool selected)
    {
        if (selected)
        {
            directionModifier = value;
            string dirName = string.IsNullOrEmpty(value) ? "None" : value;
            capi.ShowChatMessage($"Direction modifier set to: {dirName}");
        }
    }

    private void AddToRecentBlocks(string blockCode)
    {
        if (string.IsNullOrEmpty(blockCode) || blockCode == "air" || blockCode == "player")
            return;

        recentBlocks.Remove(blockCode);
        recentBlocks.Insert(0, blockCode);

        if (recentBlocks.Count > MaxRecentBlocks)
        {
            recentBlocks.RemoveAt(recentBlocks.Count - 1);
        }
    }

    private bool OnRecentBlockClicked(string blockCode)
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

        codeBlockSelected = blockCode;
        recentBlocks.Remove(blockCode);
        recentBlocks.Insert(0, blockCode);

        capi.ShowChatMessage($"Selected: {charBlockSelected} - {blockCode}");
        return true;
    }

    private string GetBlockDisplayNameForCode(string blockCode)
    {
        string baseCode = blockCode.Split('|')[0];

        var block = capi.World.GetBlock(new AssetLocation(baseCode));
        if (block != null)
        {
            try
            {
                string name = block.GetHeldItemName(new Vintagestory.API.Common.ItemStack(block));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (blockCode.Contains("|"))
                    {
                        string modifier = blockCode.Substring(blockCode.IndexOf('|'));
                        return $"{name} {modifier}";
                    }
                    return name;
                }
            }
            catch { }
        }

        return blockCode.Replace("game:", "");
    }

    private string GetBlockCategory(string blockCode, string displayName)
    {
        string lower = (blockCode + " " + displayName).ToLowerInvariant();

        if (lower.Contains("stone") || lower.Contains("rock") || lower.Contains("cobble") ||
            lower.Contains("brick") || lower.Contains("granite") || lower.Contains("marble") ||
            lower.Contains("basalt") || lower.Contains("slate") || lower.Contains("clay"))
            return "Stone";

        if (lower.Contains("wood") || lower.Contains("plank") || lower.Contains("log") ||
            lower.Contains("fence") || lower.Contains("barrel") || lower.Contains("crate") ||
            lower.Contains("door") || lower.Contains("trapdoor") || lower.Contains("ladder"))
            return "Wood";

        if (lower.Contains("soil") || lower.Contains("dirt") || lower.Contains("grass") ||
            lower.Contains("gravel") || lower.Contains("sand") || lower.Contains("peat") ||
            lower.Contains("farmland") || lower.Contains("forest"))
            return "Soil";

        if (lower.Contains("iron") || lower.Contains("copper") || lower.Contains("steel") ||
            lower.Contains("bronze") || lower.Contains("gold") || lower.Contains("silver") ||
            lower.Contains("metal") || lower.Contains("anvil"))
            return "Metal";

        if (lower.Contains("glass") || lower.Contains("window") || lower.Contains("quartz"))
            return "Glass";

        if (lower.Contains("lantern") || lower.Contains("torch") || lower.Contains("candle") ||
            lower.Contains("lamp") || lower.Contains("light") || lower.Contains("carpet") ||
            lower.Contains("rug") || lower.Contains("painting") || lower.Contains("banner") ||
            lower.Contains("pot") || lower.Contains("vase") || lower.Contains("flower"))
            return "Decor";

        if (lower.Contains("chest") || lower.Contains("hopper") || lower.Contains("chute") ||
            lower.Contains("lever") || lower.Contains("button") || lower.Contains("machine") ||
            lower.Contains("crucible") || lower.Contains("forge") || lower.Contains("bellows") ||
            lower.Contains("helve") || lower.Contains("pulverizer") || lower.Contains("quern"))
            return "Func";

        return "All";
    }

    private bool OnCategoryClicked(string category)
    {
        selectedCategory = category;
        FilterBlocks();
        RefreshGrid();
        return true;
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
        string fullBlockCode = blockCode;
        if (!string.IsNullOrEmpty(directionModifier) && blockCode != "air" && blockCode != "player")
        {
            fullBlockCode = blockCode + directionModifier;
        }

        if (character == '\0' || !mapBlocks.ContainsKey(character))
        {
            char existingChar = mapBlocks.FirstOrDefault(kvp => kvp.Value == fullBlockCode).Key;
            if (existingChar != '\0')
            {
                charBlockSelected = existingChar;
            }
            else
            {
                charBlockSelected = GetNextAvailableCharacter();
                mapBlocks[charBlockSelected] = fullBlockCode;
            }
        }
        else
        {
            charBlockSelected = character;
            if (mapBlocks.ContainsKey(character) && mapBlocks[character] != fullBlockCode)
            {
                charBlockSelected = GetNextAvailableCharacter();
                mapBlocks[charBlockSelected] = fullBlockCode;
            }
        }

        codeBlockSelected = fullBlockCode;
        AddToRecentBlocks(fullBlockCode);
        capi.ShowChatMessage($"Selected: {charBlockSelected} - {fullBlockCode}");
        return true;
    }

    private bool OnGridCellClicked(int x, int y)
    {
        if (sampleMode)
        {
            char cellChar = listSlices[indexSliceCurrent][y, x];

            if (cellChar == '_')
            {
                capi.ShowChatMessage("Empty cell - nothing to sample");
                return true;
            }

            if (mapBlocks.ContainsKey(cellChar))
            {
                charBlockSelected = cellChar;
                codeBlockSelected = mapBlocks[cellChar];
                capi.ShowChatMessage($"Sampled: {charBlockSelected} - {codeBlockSelected}");
                capi.Logger.Notification($"Sampled block from ({x}, {y}): {charBlockSelected} = {codeBlockSelected}");
            }
            else
            {
                capi.ShowChatMessage($"Cell contains unmapped character '{cellChar}'");
            }

            return true;
        }

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
        var missingMappings = new List<char>();

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
            else
            {
                missingMappings.Add(c);
            }
        }

        if (missingMappings.Count > 0)
        {
            string missingChars = string.Join(", ", missingMappings);
            capi.ShowChatMessage($"Error: Pattern contains unmapped characters: {missingChars}. These blocks were not selected from the block picker.");
            capi.Logger.Error($"PatternEditor: Missing block mappings for characters: {missingChars}");
            return true;
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

    private bool OnExportJSON()
    {
        try
        {
            var pattern = BuildPatternFromCurrentState();
            if (pattern == null)
            {
                capi.ShowChatMessage("Cannot export: pattern validation failed");
                return true;
            }

            string json = JsonConvert.SerializeObject(pattern, Formatting.Indented);
            capi.Forms.SetClipboardText(json);
            capi.ShowChatMessage("Pattern JSON copied to clipboard!");
            capi.Logger.Notification($"PatternEditor: Exported pattern to clipboard ({json.Length} characters)");
        }
        catch (Exception ex)
        {
            capi.ShowChatMessage($"Export failed: {ex.Message}");
            capi.Logger.Error($"PatternEditor: Export failed: {ex}");
        }

        return true;
    }

    private bool OnImportJSON()
    {
        try
        {
            string json = capi.Forms.GetClipboardText();
            if (string.IsNullOrWhiteSpace(json))
            {
                capi.ShowChatMessage("Clipboard is empty");
                return true;
            }

            var pattern = JsonConvert.DeserializeObject<PatternDefinition>(json);
            if (pattern == null)
            {
                capi.ShowChatMessage("Invalid JSON format");
                return true;
            }

            var errors = pattern.GetValidationErrors(capi);
            if (errors.Count > 0)
            {
                string errorMsg = string.Join(", ", errors);
                capi.ShowChatMessage($"Pattern validation failed: {errorMsg}");
                return true;
            }

            patternName = pattern.Name;
            patternDescription = pattern.Description ?? "";
            patternMode = pattern.Mode ?? "adaptive";
            gridWidth = pattern.Width;
            gridHeight = pattern.Height;

            LoadPatternIntoGrid(pattern);
            RefreshGrid();

            capi.ShowChatMessage($"Imported pattern: {patternName}");
            capi.Logger.Notification($"PatternEditor: Imported pattern from clipboard");
        }
        catch (Exception ex)
        {
            capi.ShowChatMessage($"Import failed: {ex.Message}");
            capi.Logger.Error($"PatternEditor: Import failed: {ex}");
        }

        return true;
    }

    private PatternDefinition BuildPatternFromCurrentState()
    {
        if (string.IsNullOrWhiteSpace(patternName))
        {
            capi.ShowChatMessage("Pattern name cannot be empty");
            return null;
        }

        if (gridWidth < 1 || gridHeight < 1)
        {
            capi.ShowChatMessage("Grid dimensions must be at least 1x1");
            return null;
        }

        string[] sliceStrings = BuildSliceStrings();

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
        var missingMappings = new List<char>();

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
            else
            {
                missingMappings.Add(c);
            }
        }

        if (missingMappings.Count > 0)
        {
            string missingChars = string.Join(", ", missingMappings);
            capi.ShowChatMessage($"Error: Pattern contains unmapped characters: {missingChars}");
            return null;
        }

        return new PatternDefinition
        {
            Name = patternName,
            Description = string.IsNullOrWhiteSpace(patternDescription) ? null : patternDescription,
            Slices = sliceStrings,
            Width = gridWidth,
            Height = gridHeight,
            Mode = patternMode,
            Blocks = cleanedBlocks
        };
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
