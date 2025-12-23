using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PatternBuilder.Preview;

public class PreviewRenderer : IRenderer
{
    private ICoreClientAPI api;
    private List<PreviewBlock> previewBlocks = new List<PreviewBlock>();
    private readonly object lockObject = new object();

    public double RenderOrder => 0.5;
    public int RenderRange => 999;

    public PreviewRenderer(ICoreClientAPI api)
    {
        this.api = api;
    }

    public void SetPreviewBlocks(List<PreviewBlock> blocks)
    {
        lock (lockObject)
        {
            ClearPreviewBlocks();
            previewBlocks = blocks ?? new List<PreviewBlock>();
        }
    }

    public void ClearPreviewBlocks()
    {
        lock (lockObject)
        {
            foreach (var block in previewBlocks)
            {
                block?.Dispose();
            }
            previewBlocks.Clear();
        }
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        // This shader approach was majorly inspired by the VanillaBuilding: Expanded mod from "dsisco"
        // https://mods.vintagestory.at/vanillabuildingexpanded - Thank you, David
        
        if (previewBlocks.Count == 0)
        {
            return;
        }

        var player = api.World.Player;
        if (player?.Entity == null)
        {
            return;
        }

        IRenderAPI rapi = api.Render;
        Vec3d cameraPos = player.Entity.CameraPos;

        IStandardShaderProgram shader = rapi.StandardShader;
        shader.Use();
    
        var atlasTexture = api.BlockTextureAtlas.AtlasTextures[0];
        shader.Tex2D = atlasTexture.TextureId;
        shader.RgbaTint = ColorUtil.WhiteArgbVec;
        shader.AlphaTest = 0.05f;
        shader.DontWarpVertices = 0;
        shader.AddRenderFlags = 0;
        shader.ExtraZOffset = 0.0001f;
        shader.RgbaAmbientIn = rapi.AmbientColor;
        shader.RgbaFogIn = rapi.FogColor;
        shader.FogMinIn = rapi.FogMin;
        shader.FogDensityIn = rapi.FogDensity;
        shader.NormalShaded = 1;
        shader.ProjectionMatrix = rapi.CurrentProjectionMatrix;
        shader.ViewMatrix = rapi.CameraMatrixOriginf;

        rapi.GlDisableCullFace();
        rapi.GlToggleBlend(true);

        lock (lockObject)
        {
            foreach (var previewBlock in previewBlocks)
            {
                if (previewBlock?.MeshRef == null)
                {
                    continue;
                }

                Vec4f lightRgba = api.World.BlockAccessor.GetLightRGBs(previewBlock.Position.X, previewBlock.Position.Y, previewBlock.Position.Z);
                shader.RgbaLightIn = lightRgba;

                double x = previewBlock.Position.X - cameraPos.X;
                double y = previewBlock.Position.Y - cameraPos.Y;
                double z = previewBlock.Position.Z - cameraPos.Z;

                float[] modelMatrix = Mat4f.Create();
                Mat4f.Translate(modelMatrix, modelMatrix, (float)x, (float)y, (float)z);
                shader.ModelMatrix = modelMatrix;

                rapi.RenderMesh(previewBlock.MeshRef);
            }
        }

        rapi.GlToggleBlend(false);
        rapi.GlEnableCullFace();

        shader.Stop();
    }

    public void Dispose()
    {
        ClearPreviewBlocks();
    }
}
