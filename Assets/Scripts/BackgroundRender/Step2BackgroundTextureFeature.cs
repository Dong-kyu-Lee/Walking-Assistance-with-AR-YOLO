using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Step2BackgroundRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private bool verboseLog = false;

    private Step2BackgroundPass pass;

    public override void Create()
    {
        pass = new Step2BackgroundPass(backgroundMaterial, verboseLog);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        Camera cam = renderingData.cameraData.camera;

        if (cam.cameraType == CameraType.Game)
        {
            pass.SetTargets(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera cam = renderingData.cameraData.camera;

        if (backgroundMaterial == null)
            return;

        if (cam.cameraType != CameraType.Game)
            return;

        renderer.EnqueuePass(pass);
    }

    private class Step2BackgroundPass : ScriptableRenderPass
    {
        private readonly Material material;
        private readonly bool verboseLog;

        private RTHandle colorTarget;
        private RTHandle depthTarget;

        public Step2BackgroundPass(Material material, bool verboseLog)
        {
            this.material = material;
            this.verboseLog = verboseLog;

            // 지금은 skybox 영향 줄이기 위해 이 시점 유지
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }

        public void SetTargets(RTHandle colorTarget, RTHandle depthTarget)
        {
            this.colorTarget = colorTarget;
            this.depthTarget = depthTarget;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(colorTarget, depthTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || colorTarget == null)
                return;

            if (verboseLog)
                Debug.Log("[Step2BackgroundPass] Execute");

            CommandBuffer cmd = CommandBufferPool.Get("Step2 Background Pass");

            CoreUtils.SetRenderTarget(cmd, colorTarget, depthTarget);
            CoreUtils.DrawFullScreen(cmd, material);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}