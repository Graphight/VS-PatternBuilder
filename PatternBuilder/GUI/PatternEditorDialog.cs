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

    private char[,] grid;
    private char selectedBlockChar = 'A';
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
            ('A', "air", "Air"),
            ('B', "game:cobblestone-granite", "Cobblestone"),
            ('C', "game:stonebricks-granite", "Stone Bricks"),
            ('D', "game:soil-medium-normal", "Soil"),
            ('E', "game:gravel-granite", "Gravel"),
            ('F', "game:log-placed-oak-ud", "Oak Log"),
            ('G', "game:planks-oak", "Oak Planks"),
            ('H', "game:claybricks-fire", "Clay Bricks"),
            ('I', "game:glass-plain", "Glass"),
            ('J', "game:sand-granite", "Sand"),
            ('K', "game:stonepath", "Stone Path"),
            ('L', "game:torch-basic-lit-up", "Torch"),
            ('M', "game:lantern-iron-on", "Lantern"),
            ('N', "game:woodenfence-oak-empty-free", "Fence"),
            ('O', "game:door-wood-oak-left-closed-n", "Door"),
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
        grid = new char[gridHeight, gridWidth];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                grid[y, x] = 'A';
            }
        }

        if (gridWidth >= 1 && gridHeight >= 1)
        {
            grid[0, gridWidth / 2] = 'P';
        }
    }

    private void LoadPatternIntoGrid(PatternDefinition pattern)
    {
        grid = new char[gridHeight, gridWidth];
        blockMappings = new Dictionary<char, string>(pattern.Blocks);

        if (pattern.ParsePattern(0))
        {
            for (int y = 0; y < gridHeight && y < pattern.Height; y++)
            {
                for (int x = 0; x < gridWidth && x < pattern.Width; x++)
                {
                    string blockCode = pattern.GetBlockAt(x, y);

                    if (blockCode == "air" || blockCode == null)
                    {
                        grid[y, x] = '_';
                    }
                    else
                    {
                        char foundChar = '_';
                        foreach (var kvp in pattern.Blocks)
                        {
                            if (kvp.Value == blockCode)
                            {
                                foundChar = kvp.Key;
                                break;
                            }
                        }
                        grid[y, x] = foundChar;
                    }
                }
            }
        }
        else
        {
            InitializeEmptyGrid();
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
        currentY += 50;

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
                .AddStaticText("Grid Editor - Coming Soon", CairoFont.WhiteSmallText(), gridContainerBounds)
                .AddInset(blockPickerInsetBounds, 3)
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
            InitializeEmptyGrid();
        }
    }

    private void OnHeightChanged(string value)
    {
        if (int.TryParse(value, out int height) && height > 0 && height <= 15)
        {
            gridHeight = height;
            InitializeEmptyGrid();
        }
    }

    private bool OnBlockSelected(char character, string blockCode)
    {
        selectedBlockChar = character;
        selectedBlockCode = blockCode;
        capi.ShowChatMessage($"Selected: {character} - {blockCode}");
        return true;
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
        capi.ShowChatMessage("Save functionality - Coming Soon");
        return true;
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
