using System.Collections.Generic;
using Vintagestory.API.Common;

namespace PatternBuilder.Pattern;

public class PatternManager
{
    public const int MaxSlots = 50;

    private readonly ICoreAPI api;
    private readonly Dictionary<int, PatternDefinition> patterns;
    private int currentSlot;
    private int currentSliceIndex;
    private PatternDefinition fallbackPattern;

    public PatternManager(ICoreAPI api)
    {
        this.api = api;
        this.patterns = new Dictionary<int, PatternDefinition>();
        this.currentSlot = 1;
        this.fallbackPattern = PatternDefinition.CreateHardcodedDefault();
    }

    public void LoadPatterns(Dictionary<int, PatternDefinition> loadedPatterns)
    {
        patterns.Clear();

        foreach (var kvp in loadedPatterns)
        {
            patterns[kvp.Key] = kvp.Value;
        }

        api.Logger.Notification($"PatternBuilder: Loaded {patterns.Count} patterns into manager");

        if (!patterns.ContainsKey(currentSlot))
        {
            currentSlot = FindFirstAvailableSlot();
        }
    }

    public bool SwitchToSlot(int slot)
    {
        if (slot < 1 || slot > MaxSlots)
        {
            api.Logger.Warning($"PatternBuilder: Invalid slot number: {slot}");
            return false;
        }

        if (!patterns.ContainsKey(slot))
        {
            api.Logger.Warning($"PatternBuilder: No pattern in slot {slot}");
            return false;
        }

        currentSlot = slot;
        ResetSliceIndex();
        api.Logger.Debug($"PatternBuilder: Switched to slot {slot}: {GetCurrentPattern().Name}");
        return true;
    }

    public PatternDefinition GetCurrentPattern()
    {
        if (patterns.TryGetValue(currentSlot, out var pattern))
        {
            return pattern;
        }

        api.Logger.Warning($"PatternBuilder: Current slot {currentSlot} has no pattern, using fallback");
        return fallbackPattern;
    }

    public int GetCurrentSlot()
    {
        return currentSlot;
    }

    public bool HasPatternInSlot(int slot)
    {
        return patterns.ContainsKey(slot);
    }

    public string GetPatternNameInSlot(int slot)
    {
        if (patterns.TryGetValue(slot, out var pattern))
        {
            return pattern.Name;
        }

        return null;
    }

    private int FindFirstAvailableSlot()
    {
        for (int i = 1; i <= MaxSlots; i++)
        {
            if (patterns.ContainsKey(i))
            {
                return i;
            }
        }

        return 1;
    }

    public Dictionary<int, string> GetAllPatternNames()
    {
        var names = new Dictionary<int, string>();

        for (int i = 1; i <= MaxSlots; i++)
        {
            if (patterns.TryGetValue(i, out var pattern))
            {
                names[i] = pattern.Name;
            }
        }

        return names;
    }

    public int GetCurrentSliceIndex()
    {
        return currentSliceIndex;
    }

    public void IncrementSliceIndex()
    {
        var pattern = GetCurrentPattern();
        int depth = pattern.GetDepth();

        if (depth > 0)
        {
            currentSliceIndex = (currentSliceIndex + 1) % depth;
            api.Logger.Debug($"PatternBuilder: Incremented slice to {currentSliceIndex}/{depth}");
        }
    }

    public void DecrementSliceIndex()
    {
        var pattern = GetCurrentPattern();
        int depth = pattern.GetDepth();

        if (depth > 0)
        {
            currentSliceIndex = (currentSliceIndex - 1 + depth) % depth;
            api.Logger.Debug($"PatternBuilder: Decremented slice to {currentSliceIndex}/{depth}");
        }
    }

    public void ResetSliceIndex()
    {
        currentSliceIndex = 0;
        api.Logger.Debug("PatternBuilder: Reset slice index to 0");
    }
}