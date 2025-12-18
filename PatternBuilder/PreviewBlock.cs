using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

public class PreviewBlock : IDisposable
{
    public BlockPos Position { get; private set; }
    public Block Block { get; private set; }
    public int TintColor { get; private set; }
    public MeshRef MeshRef { get; private set; }

    private ICoreClientAPI api;
    private bool disposed = false;

    public PreviewBlock(BlockPos position, Block block, int tintColor, ICoreClientAPI api)
    {
        this.Position = position;
        this.Block = block;
        this.TintColor = tintColor;
        this.api = api;
    }

    public bool UploadMesh()
    {
        if (Block == null || api == null)
        {
            api?.World.Logger.Warning($"PreviewBlock: UploadMesh called with null Block or API");
            return false;
        }

        try
        {
            MeshData meshData = GetBlockMesh();
            if (meshData == null)
            {
                api.World.Logger.Warning($"PreviewBlock: GetBlockMesh returned null for {Block.Code}");
                return false;
            }

            ApplyTintToMesh(meshData);
            MeshRef = api.Render.UploadMesh(meshData);

            if (MeshRef == null)
            {
                api.World.Logger.Warning($"PreviewBlock: UploadMesh returned null MeshRef for {Block.Code}");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            api.World.Logger.Error($"PreviewBlock: Failed to upload mesh for block {Block.Code}: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    private MeshData GetBlockMesh()
    {
        ITesselatorAPI tesselator = api.Tesselator;
        MeshData meshData;

        if (Block.Code.Path == "air")
        {
            Block glassBlock = api.World.GetBlock(new AssetLocation("game:glass-plain"));
            if (glassBlock != null)
            {
                tesselator.TesselateBlock(glassBlock, out meshData);
            }
            else
            {
                tesselator.TesselateBlock(Block, out meshData);
            }
        }
        else
        {
            tesselator.TesselateBlock(Block, out meshData);
        }

        return meshData;
    }

    private void ApplyTintToMesh(MeshData meshData)
    {
        if (meshData?.Rgba == null)
        {
            return;
        }
        
        // Cheeky bit shifting to extract colour channels (colours are 32-bit integers)
        // Binary:    11111111 00110010 01100100 11001000 | ALPHA RED GREEN BLUE
        int componentA = (TintColor >> 24) & 0xFF;
        int componentR = (TintColor >> 16) & 0xFF;
        int componentG = (TintColor >> 8) & 0xFF;
        int componentB = TintColor & 0xFF;

        for (int i = 0; i < meshData.Rgba.Length; i += 4)
        {
            meshData.Rgba[i] = (byte)((meshData.Rgba[i] * componentR) / 255);
            meshData.Rgba[i + 1] = (byte)((meshData.Rgba[i + 1] * componentG) / 255);
            meshData.Rgba[i + 2] = (byte)((meshData.Rgba[i + 2] * componentB) / 255);
            meshData.Rgba[i + 3] = (byte)Math.Min(meshData.Rgba[i + 3], componentA);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (MeshRef != null)
        {
            api.Render.DeleteMesh(MeshRef);
            MeshRef = null;
        }

        disposed = true;
    }
}
