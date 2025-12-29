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

    public override string ToggleKeyCombinationCode => "patternbrowser";

    public PatternBrowserDialog(ICoreClientAPI capi, PatternManager patternManager, Action<int> onPatternSelected) : base(capi)
    {
        this.patternManager = patternManager;
        this.onPatternSelected = onPatternSelected;
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        SetupDialog();
    }

    private void SetupDialog()
    {
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        ElementBounds scrollBounds = ElementBounds.Fixed(0, 30, 500, 400);
        ElementBounds clipBounds = scrollBounds.ForkBoundingParent();
        ElementBounds insetBounds = scrollBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

        ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(insetBounds.fixedWidth + 7, 0, 0, 0)
            .WithFixedWidth(20);

        var composer = capi.Gui.CreateCompo("patternbrowser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Pattern Browser", OnClose)
            .BeginChildElements(bgBounds)
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

            if (hasPattern)
            {
                var pattern = patternManager.GetPatternInSlot(slot);
                if (pattern != null)
                {
                    string mode = pattern.Mode == "carve" ? "C" : "A";
                    patternInfo = $"{pattern.Name} ({pattern.Width}x{pattern.Height}x{pattern.GetDepth()}) [{mode}]";
                }
            }

            ElementBounds rowBounds = ElementBounds.Fixed(0, currentY, 480, 25);

            string prefix = isCurrent ? "> " : "  ";
            string displayText = $"{prefix}{slotText}: {patternInfo}";

            CairoFont font = isCurrent
                ? CairoFont.WhiteDetailText().WithWeight(Cairo.FontWeight.Bold)
                : CairoFont.WhiteDetailText();

            int capturedSlot = slot;

            composer.AddButton(displayText, () => OnPatternRowClicked(capturedSlot), rowBounds, font, EnumButtonStyle.MainMenu, $"btn-slot-{slot}");

            currentY += 27;
        }

        composer.EndClip()
            .EndChildElements()
            .Compose();

        SingleComposer = composer;
    }

    private bool OnPatternRowClicked(int slot)
    {
        capi.Logger.Notification($"Pattern browser: clicked slot {slot}");
        onPatternSelected?.Invoke(slot);
        TryClose();
        return true;
    }

    private void OnClose()
    {
        TryClose();
    }
}
