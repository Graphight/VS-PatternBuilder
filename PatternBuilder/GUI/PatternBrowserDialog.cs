using System;
using System.Collections.Generic;
using System.Linq;
using PatternBuilder.Pattern;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PatternBuilder.GUI;

public class PatternBrowserDialog : GuiDialog
{
    private readonly PatternManager patternManager;
    private Action<int> onPatternSelected;
    private Action onReloadRequested;
    private string searchText = "";
    private int selectedSlot = -1;
    private Dictionary<int, double> buttonOriginalY = new Dictionary<int, double>();

    public override string ToggleKeyCombinationCode => "patternbrowser";

    public PatternBrowserDialog(ICoreClientAPI capi, PatternManager patternManager, Action<int> onPatternSelected, Action onReloadRequested) : base(capi)
    {
        this.patternManager = patternManager;
        this.onPatternSelected = onPatternSelected;
        this.onReloadRequested = onReloadRequested;
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        searchText = "";
        SetupDialog();
    }

    private void SetupDialog()
    {
        buttonOriginalY.Clear();

        ElementBounds boundsDialog = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        ElementBounds boundsBg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        boundsBg.BothSizing = ElementSizing.FitToChildren;

        ElementBounds boundsSearchLabel = ElementBounds.Fixed(0, 35, 60, 25);
        ElementBounds boundsSearchInput = ElementBounds.Fixed(65, 30, 415, 30);
        ElementBounds boundsScroll = ElementBounds.Fixed(0, 70, 480, 240);
        ElementBounds boundsClip = boundsScroll.ForkBoundingParent();
        ElementBounds boundsInset = boundsScroll.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

        ElementBounds boundsInfoPanel = ElementBounds.Fixed(0, 320, 460, 160);
        ElementBounds boundsInfoClip = boundsInfoPanel.ForkBoundingParent();
        ElementBounds boundsInfoInset = boundsInfoPanel.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

        ElementBounds boundsSelect = ElementBounds.Fixed(210, 490, 200, 30);
        ElementBounds boundsReload = ElementBounds.Fixed(0, 490, 200, 30);

        ElementBounds boundsScrollbar = boundsInset.CopyOffsetedSibling(boundsInset.fixedWidth + 7, 0, 0, 0)
            .WithFixedWidth(20);

        ElementBounds boundsInfoScrollbar = boundsInfoInset.CopyOffsetedSibling(boundsInfoInset.fixedWidth + 7, 0, 0, 0)
            .WithFixedWidth(20);

        var composer = capi.Gui.CreateCompo("patternbrowser", boundsDialog)
            .AddShadedDialogBG(boundsBg)
            .AddDialogTitleBar("Pattern Browser", OnClose)
            .BeginChildElements(boundsBg)
                .AddStaticText("Search:", CairoFont.WhiteSmallText(), boundsSearchLabel)
                .AddTextInput(boundsSearchInput, OnSearchChanged, CairoFont.WhiteDetailText(), "search-input")
                .AddInset(boundsInset, 3)
                .BeginClip(boundsClip);

        int currentSlot = patternManager.GetCurrentSlot();
        double currentY = 0;

        for (int slot = 1; slot <= PatternManager.MaxSlots; slot++)
        {
            bool hasPattern = patternManager.HasPatternInSlot(slot);
            bool isCurrent = slot == currentSlot;

            string slotText = $"Slot {slot}";
            string patternInfo = "Empty";
            string patternName = "";
            string validationIcon = " ";

            if (hasPattern)
            {
                var pattern = patternManager.GetPatternInSlot(slot);
                if (pattern != null)
                {
                    patternName = pattern.Name;
                    string mode = pattern.Mode == "carve" ? "C" : "A";
                    patternInfo = $"{pattern.Name} ({pattern.Width}x{pattern.Height}x{pattern.GetDepth()}) [{mode}]";

                    var errors = pattern.GetValidationErrors(capi);
                    if (errors.Count == 0)
                    {
                        validationIcon = "✓";
                    }
                    else
                    {
                        validationIcon = "✗";
                    }
                }
            }

            if (!string.IsNullOrEmpty(searchText) && !patternName.ToLowerInvariant().Contains(searchText.ToLowerInvariant()) && patternInfo != "Empty")
            {
                continue;
            }

            ElementBounds boundsRow = ElementBounds.Fixed(0, currentY, 480, 25);

            string prefix = isCurrent ? "> " : "  ";
            string displayText = $"{prefix}{validationIcon} {slotText}: {patternInfo}";

            CairoFont font = isCurrent
                ? CairoFont.WhiteDetailText().WithWeight(Cairo.FontWeight.Bold)
                : CairoFont.WhiteDetailText();

            int capturedSlot = slot;

            buttonOriginalY[slot] = currentY;
            composer.AddButton(displayText, () => OnPatternRowClicked(capturedSlot), boundsRow, font, EnumButtonStyle.MainMenu, $"btn-slot-{slot}");

            currentY += 27;
        }

        composer.EndClip()
            .AddVerticalScrollbar(OnScroll, boundsScrollbar, "scrollbar")
            .AddInset(boundsInfoInset, 3)
            .BeginClip(boundsInfoClip)
            .AddDynamicText("", CairoFont.WhiteSmallText(), boundsInfoPanel.FlatCopy().WithFixedPadding(5), "info-text")
            .EndClip()
            .AddVerticalScrollbar(OnInfoScroll, boundsInfoScrollbar, "info-scrollbar")
            .AddSmallButton("Reload Patterns", OnReloadPatterns, boundsReload)
            .AddSmallButton("Select Pattern", OnSelectPattern, boundsSelect)
            .EndChildElements()
            .Compose();

        SingleComposer = composer;

        SingleComposer.GetScrollbar("scrollbar").SetHeights(
            (float)boundsScroll.fixedHeight,
            (float)(currentY)
        );

        var searchInput = SingleComposer.GetTextInput("search-input");
        if (searchInput != null)
        {
            searchInput.SetValue(searchText);
        }

        UpdateInfoPanel();
    }

    private void UpdateInfoPanel()
    {
        var infoText = SingleComposer?.GetDynamicText("info-text");
        if (infoText == null) return;

        if (selectedSlot < 1 || selectedSlot > PatternManager.MaxSlots)
        {
            infoText.SetNewText("Click a pattern to see details");
            return;
        }

        if (!patternManager.HasPatternInSlot(selectedSlot))
        {
            infoText.SetNewText($"Slot {selectedSlot}: Empty");
            return;
        }

        var pattern = patternManager.GetPatternInSlot(selectedSlot);
        if (pattern == null)
        {
            infoText.SetNewText($"Slot {selectedSlot}: Error loading pattern");
            return;
        }

        var errors = pattern.GetValidationErrors(capi);
        string info = $"{pattern.Name}\n{pattern.Description ?? "No description"}";

        var blockCounts = GetBlockCounts(pattern);
        if (blockCounts.Count > 0)
        {
            int sliceCount = pattern.GetDepth();
            string cycleText = sliceCount > 1 ? $"cycle ({sliceCount} slices)" : "placement";
            info += $"\n\nBlocks per {cycleText}:";
            foreach (var kvp in blockCounts.OrderByDescending(x => x.Value))
            {
                info += $"\n- {kvp.Key}: {kvp.Value}";
            }
        }

        if (errors.Count > 0)
        {
            info += $"\n\nErrors:\n- {string.Join("\n- ", errors)}";
        }

        infoText.SetNewText(info);

        var infoScrollbar = SingleComposer?.GetScrollbar("info-scrollbar");
        if (infoScrollbar != null)
        {
            double textHeight = infoText.Font.GetTextExtents(info).Height / RuntimeEnv.GUIScale;
            infoScrollbar.SetHeights(160f, (float)textHeight);
        }
    }

    private Dictionary<string, int> GetBlockCounts(PatternDefinition pattern)
    {
        var counts = new Dictionary<string, int>();
        int sliceCount = pattern.GetDepth();

        for (int sliceIdx = 0; sliceIdx < sliceCount; sliceIdx++)
        {
            if (!pattern.ParsePattern(sliceIdx))
                continue;

            for (int y = 0; y < pattern.Height; y++)
            {
                for (int x = 0; x < pattern.Width; x++)
                {
                    string blockCode = pattern.GetBlockAt(x, y);
                    if (blockCode == null || blockCode == "air")
                        continue;

                    string displayName = GetBlockDisplayName(blockCode);
                    if (!counts.ContainsKey(displayName))
                        counts[displayName] = 0;
                    counts[displayName]++;
                }
            }
        }

        return counts;
    }

    private string GetBlockDisplayName(string blockCode)
    {
        if (blockCode.Contains("*"))
        {
            var parts = blockCode.Split('-');
            if (parts.Length > 0)
            {
                string blockType = parts[0].Replace("game:", "").Replace("*", "any");
                return char.ToUpper(blockType[0]) + blockType.Substring(1);
            }
        }

        var block = capi.World.GetBlock(new AssetLocation(blockCode.Split('|')[0]));
        if (block != null)
        {
            return block.GetHeldItemName(new Vintagestory.API.Common.ItemStack(block));
        }

        return blockCode.Replace("game:", "");
    }

    private bool OnPatternRowClicked(int slot)
    {
        selectedSlot = slot;
        UpdateInfoPanel();
        return true;
    }

    private bool OnSelectPattern()
    {
        if (selectedSlot < 1 || selectedSlot > PatternManager.MaxSlots)
        {
            capi.ShowChatMessage("Please select a pattern first");
            return true;
        }

        if (!patternManager.HasPatternInSlot(selectedSlot))
        {
            capi.ShowChatMessage($"Slot {selectedSlot} is empty");
            return true;
        }

        capi.Logger.Notification($"Pattern browser: selected slot {selectedSlot}");
        onPatternSelected?.Invoke(selectedSlot);
        TryClose();
        return true;
    }

    private void OnSearchChanged(string text)
    {
        if (searchText == text) return;
        searchText = text;

        capi.Event.EnqueueMainThreadTask(() => {
            SetupDialog();
        }, "pattern-browser-filter");
    }

    private void OnScroll(float value)
    {
        foreach (var kvp in buttonOriginalY)
        {
            int slot = kvp.Key;
            double originalY = kvp.Value;

            var button = SingleComposer.GetButton($"btn-slot-{slot}");
            if (button != null)
            {
                button.Bounds.fixedY = originalY - value;
                button.Bounds.CalcWorldBounds();
            }
        }
    }

    private void OnInfoScroll(float value)
    {
        var infoText = SingleComposer?.GetDynamicText("info-text");
        if (infoText != null)
        {
            infoText.Bounds.fixedY = 0 - value;
            infoText.Bounds.CalcWorldBounds();
        }
    }

    private bool OnReloadPatterns()
    {
        onReloadRequested?.Invoke();
        SetupDialog();
        return true;
    }

    private void OnClose()
    {
        TryClose();
    }
}
