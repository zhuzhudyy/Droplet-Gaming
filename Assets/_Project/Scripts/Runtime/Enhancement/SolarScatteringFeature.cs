using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DropletPrototype
{
    /// <summary>URP 17 RenderGraph: depth-clipped half-resolution ray integration, then additive upsample.</summary>
    public sealed class SolarScatteringFeature : ScriptableRendererFeature
    {
        public Shader scatteringShader;
        Material material;
        ScatterPass pass;
        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (scatteringShader != null) material = CoreUtils.CreateEngineMaterial(scatteringShader);
            pass = new ScatterPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (material == null || data.cameraData.cameraType != CameraType.Game) return;
            var volume = data.cameraData.camera.GetComponent<SolarScatteringVolume>();
            if (volume == null || !volume.IsVisible(data.cameraData.camera)) return;
            pass.material = material; pass.volume = volume;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }
        sealed class ScatterPass : ScriptableRenderPass
        {
            public Material material;
            public SolarScatteringVolume volume;
            sealed class Data { public TextureHandle source, depth; public UniversalCameraData camera; public Material material; public int shaderPass; public Vector4 sphere, parameters, color; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
            {
                var resources = context.Get<UniversalResourceData>();
                var camera = context.Get<UniversalCameraData>();
                if (!resources.cameraDepthTexture.IsValid()) return;
                var desc = camera.cameraTargetDescriptor;
                desc.width = Mathf.Max(1, desc.width / 2); desc.height = Mathf.Max(1, desc.height / 2);
                desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                var half = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Local Corona Half Resolution", false);
                var center = volume.sun.sunProxy.position;
                var sphere = new Vector4(center.x, center.y, center.z, volume.sun.DisplayRadiusUnits * volume.outerRadiusMultiplier);
                var parameters = new Vector4(volume.sun.DisplayRadiusUnits, volume.opticalDepth, Mathf.Clamp(volume.samples, 8, 32), 0);
                using (var builder = graph.AddRasterRenderPass<Data>("Solar Corona Ray Integration (half)", out var data))
                {
                    data.source = resources.cameraDepthTexture; data.material = material; data.shaderPass = 0;
                    data.depth = resources.cameraDepthTexture; data.camera = camera;
                    data.sphere = sphere; data.parameters = parameters; data.color = volume.scatteringColor;
                    builder.UseTexture(data.source, AccessFlags.Read); builder.SetRenderAttachment(half, 0, AccessFlags.Write);
                    builder.SetRenderFunc((Data d, RasterGraphContext ctx) => Draw(d, ctx));
                }
                using (var builder = graph.AddRasterRenderPass<Data>("Solar Corona Depth-aware Composite", out var data))
                {
                    data.source = half; data.material = material; data.shaderPass = 1;
                    data.depth = resources.cameraDepthTexture; data.camera = camera;
                    data.sphere = sphere; data.parameters = parameters; data.color = volume.scatteringColor;
                    builder.UseTexture(half, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((Data d, RasterGraphContext ctx) => Draw(d, ctx));
                }
            }
            static void Draw(Data data, RasterGraphContext context)
            {
                var origin = context.GetTextureUVOrigin(data.depth);
                var projection = GL.GetGPUProjectionMatrix(data.camera.GetProjectionMatrix(), origin == TextureUVOrigin.BottomLeft);
                data.material.SetMatrix("_DepthInvVP", (projection * data.camera.GetViewMatrix()).inverse);
                data.material.SetVector("_CoronaSphere", data.sphere);
                data.material.SetVector("_CoronaParameters", data.parameters);
                data.material.SetVector("_CoronaColor", data.color);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
            }
        }
    }
}
