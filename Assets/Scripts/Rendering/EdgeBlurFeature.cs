using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class EdgeBlurFeature : ScriptableRendererFeature
{
    private static readonly int RadiusId = Shader.PropertyToID("_EdgeBlurRadius");
    private static readonly int SharpAreaId = Shader.PropertyToID("_EdgeBlurSharpArea");
    private static readonly int FalloffId = Shader.PropertyToID("_EdgeBlurFalloff");

    [SerializeField] private Shader shader;

    private Material material;
    private EdgeBlurPass pass;

    public override void Create()
    {
        pass = new EdgeBlurPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game || shader == null) return;

        EdgeBlur settings = VolumeManager.instance.stack.GetComponent<EdgeBlur>();
        if (settings == null || !settings.IsActive()) return;

        if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
        material.SetFloat(RadiusId, settings.radius.value * settings.intensity.value * 0.001f);
        material.SetFloat(SharpAreaId, settings.sharpArea.value);
        material.SetFloat(FalloffId, settings.falloff.value);
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    private sealed class EdgeBlurPass : ScriptableRenderPass
    {
        private Material material;

        public void Setup(Material blurMaterial)
        {
            material = blurMaterial;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || material == null) return;

            TextureHandle source = resources.activeColorTexture;
            TextureDesc description = renderGraph.GetTextureDesc(source);
            description.name = "_EdgeBlur";
            description.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(description);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Edge Blur");
            resources.cameraColor = destination;
        }
    }
}
