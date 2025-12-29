using System;
using System.Collections.Generic;
using PatternBuilder.Pattern;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace PatternBuilder.GUI;

public class PatternBrowserDialog : GuiDialog
{
    private readonly PatternManager patternManager;
    private Action<int> onPatternSelected;
    private Action onReloadRequested;
    private string searchText = "";

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
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        ElementBounds searchLabelBounds = ElementBounds.Fixed(0, 35, 60, 25);
        ElementBounds searchInputBounds = ElementBounds.Fixed(65, 30, 415, 30);
        ElementBounds scrollBounds = ElementBounds.Fixed(0, 70, 480, 320);
        ElementBounds clipBounds = scrollBounds.ForkBoundingParent();
        ElementBounds insetBounds = scrollBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
        ElementBounds reloadButtonBounds = ElementBounds.Fixed(0, 400, 200, 30);

        ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(insetBounds.fixedWidth + 7, 0, 0, 0)
            .WithFixedWidth(20);

        var composer = capi.Gui.CreateCompo("patternbrowser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Pattern Browser", OnClose)
            .BeginChildElements(bgBounds)
                .AddStaticText("Search:", CairoFont.WhiteSmallText(), searchLabelBounds)
                .AddTextInput(searchInputBounds, OnSearchChanged, CairoFont.WhiteDetailText(), "search-input")
                .AddInset(insetBounds, 3)
                .BeginClip(clipBounds);

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

            ElementBounds rowBounds = ElementBounds.Fixed(0, currentY, 480, 25);

            string prefix = isCurrent ? "> " : "  ";
            string displayText = $"{prefix}{validationIcon} {slotText}: {patternInfo}";

            CairoFont font = isCurrent
                ? CairoFont.WhiteDetailText().WithWeight(Cairo.FontWeight.Bold)
                : CairoFont.WhiteDetailText();

            int capturedSlot = slot;

            composer.AddButton(displayText, () => OnPatternRowClicked(capturedSlot), rowBounds, font, EnumButtonStyle.MainMenu, $"btn-slot-{slot}");

            currentY += 27;
        }

        composer.EndClip()
            .AddSmallButton("Reload Patterns", OnReloadPatterns, reloadButtonBounds)
            .EndChildElements()
            .Compose();

        SingleComposer = composer;

        var searchInput = SingleComposer.GetTextInput("search-input");
        if (searchInput != null)
        {
            searchInput.SetValue(searchText);
        }
    }

    private bool OnPatternRowClicked(int slot)
    {
        capi.Logger.Notification($"Pattern browser: clicked slot {slot}");
        onPatternSelected?.Invoke(slot);
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
