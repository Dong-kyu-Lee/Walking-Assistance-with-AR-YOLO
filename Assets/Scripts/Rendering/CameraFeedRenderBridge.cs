using UnityEngine;

public enum FeedSourceMode
{
    CameraX,
    DemoVideo
}

public enum FeedAspectTarget
{
    FullScreen,
    StereoEye
}

[DefaultExecutionOrder(50)]
public class CameraFeedRenderBridge : MonoBehaviour
{
    [Header("Frame Source")]
    [SerializeField] private MonoBehaviour frameSourceBehaviour;

    [Header("Texture Options")]
    [SerializeField] private bool flipY = true;
    [SerializeField] private bool flipDemoVideoY = false;
    [SerializeField] private bool useAspectCrop = true;
    [SerializeField] private FeedAspectTarget aspectTarget = FeedAspectTarget.FullScreen;

    [Header("Feed Source")]
    [SerializeField] private FeedSourceMode feedSourceMode = FeedSourceMode.CameraX;

    [Header("Demo Video Input")]
    [SerializeField] private VideoFeedToRenderFeature videoFeed;

    private ICameraFrameSource frameSource;

    public FeedSourceMode CurrentFeedSourceMode => feedSourceMode;
    public bool IsDemoVideoMode => feedSourceMode == FeedSourceMode.DemoVideo;

    private void Awake()
    {
        frameSource = frameSourceBehaviour as ICameraFrameSource;
        videoFeed ??= frameSourceBehaviour as VideoFeedToRenderFeature;

        if (feedSourceMode == FeedSourceMode.DemoVideo && videoFeed == null)
        {
            videoFeed = FindFirstObjectByType<VideoFeedToRenderFeature>();
        }

        if (feedSourceMode == FeedSourceMode.CameraX && frameSource == null)
        {
            Debug.LogError("frameSourceBehaviour must implement ICameraFrameSource.");
            enabled = false;
            return;
        }

        if (feedSourceMode == FeedSourceMode.DemoVideo && videoFeed == null)
        {
            Debug.LogError("DemoVideo mode requires VideoFeedToRenderFeature.");
            enabled = false;
            return;
        }
    }

    private void LateUpdate()
    {
        ICameraFrameSource activeSource = GetActiveFrameSource();

        if (activeSource == null || !activeSource.HasFrame || activeSource.FrameTexture == null)
        {
            Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 0f);
            return;
        }

        Shader.SetGlobalTexture(CameraFeedShaderIds.CameraFeedTex, activeSource.FrameTexture);
        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 1f);
        ApplyCameraUvMode();

        float aspect = activeSource.FrameHeight > 0
            ? (float)activeSource.FrameWidth / activeSource.FrameHeight
            : 1f;

        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAspect, aspect);
        ApplyAspectCrop(activeSource.FrameWidth, activeSource.FrameHeight);

        bool shouldFlipY = feedSourceMode == FeedSourceMode.DemoVideo
            ? flipDemoVideoY
            : flipY;

        Vector4 st = shouldFlipY
            ? new Vector4(1f, -1f, 0f, 1f)
            : new Vector4(1f, 1f, 0f, 0f);

        Shader.SetGlobalVector(CameraFeedShaderIds.CameraFeedST, st);
    }

    private ICameraFrameSource GetActiveFrameSource()
    {
        return feedSourceMode == FeedSourceMode.DemoVideo
            ? videoFeed
            : frameSource;
    }

    private void ApplyCameraUvMode()
    {
        if (feedSourceMode == FeedSourceMode.DemoVideo)
        {
            Shader.SetGlobalFloat(CameraFeedShaderIds.UseXRUv, 1f);
            Shader.SetGlobalFloat(CameraFeedShaderIds.XRCameraFeedFlipY, 0f);
        }
    }

    private void ApplyAspectCrop(int width, int height)
    {
        if (!useAspectCrop || width <= 0 || height <= 0)
        {
            Shader.SetGlobalVector(CameraFeedShaderIds.CameraAspectCrop, new Vector4(1f, 1f, 0f, 0f));
            return;
        }

        float sourceAspect = (float)width / height;
        float targetAspect = Screen.width > 0 && Screen.height > 0
            ? (float)Screen.width / Screen.height
            : sourceAspect;

        if (aspectTarget == FeedAspectTarget.StereoEye && targetAspect > 1f)
        {
            targetAspect *= 0.5f;
        }

        float cropX = 1f;
        float cropY = 1f;

        if (sourceAspect > targetAspect)
        {
            cropX = targetAspect / sourceAspect;
        }
        else
        {
            cropY = sourceAspect / targetAspect;
        }

        Shader.SetGlobalVector(CameraFeedShaderIds.CameraAspectCrop, new Vector4(cropX, cropY, 0f, 0f));
    }
}
