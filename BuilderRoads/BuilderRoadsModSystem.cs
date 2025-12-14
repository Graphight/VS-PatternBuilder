using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace BuilderRoads;

public class BuilderRoadsModSystem : ModSystem
{
    private ICoreClientAPI clientApi;
    private bool roadBuildingEnabled = false;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        // Server-side functionality not needed for Phase 1
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.clientApi = api;

        // Register the hotkey: Ctrl+Shift+R
        clientApi.Input.RegisterHotKey("toggleroadbuilder", "Toggle Road Builder",
            GlKeys.R, HotkeyType.CharacterControls,
            ctrlPressed: true, shiftPressed: true);

        // Set up the hotkey handler
        clientApi.Input.SetHotKeyHandler("toggleroadbuilder", OnToggleRoadBuilder);

        Mod.Logger.Notification("BuilderRoads loaded - Press Ctrl+Shift+R to toggle");
    }

    private bool OnToggleRoadBuilder(KeyCombination key)
    {
        roadBuildingEnabled = !roadBuildingEnabled;

        string status = roadBuildingEnabled ? "enabled" : "disabled";
        clientApi.ShowChatMessage($"Road building mode {status}");

        return true;
    }
}