using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraFeedRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    [SerializeField] private Settings settings = new Settings();

    private CameraFeedRenderPass pass;

    public override void Create()
    {
        pass = new CameraFeedRenderPass(settings.material);
        pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (pass != null)
        {
            pass.Setup(renderer.cameraColorTargetHandle);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
            return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    private class CameraFeedRenderPass : ScriptableRenderPass
    {
        private readonly Material material;
        private RTHandle source;
        private RTHandle tempTexture;

        public CameraFeedRenderPass(Material material)
        {
            this.material = material;
        }

        public void Setup(RTHandle source)
        {
            this.source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(
                ref tempTexture,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_CameraFeedTempTexture"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
                return;

            if (Shader.GetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable) < 0.5f)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Camera Feed Render Pass");

            Blitter.BlitCameraTexture(cmd, source, tempTexture, material, 0);
            Blitter.BlitCameraTexture(cmd, tempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempTexture?.Release();
        }
    }
}