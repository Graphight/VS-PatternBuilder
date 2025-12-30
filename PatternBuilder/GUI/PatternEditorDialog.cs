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

        var composer = capi.Gui.CreateCompo("patterneditor", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Pattern Editor", OnClose)
            .BeginChildElements(bgBounds)
                .AddStaticText("Pattern Editor - Under Construction", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, 400, 30))
                .AddSmallButton("Close", OnCloseButton, ElementBounds.Fixed(0, 70, 100, 30))
            .EndChildElements()
            .Compose();

        SingleComposer = composer;
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
