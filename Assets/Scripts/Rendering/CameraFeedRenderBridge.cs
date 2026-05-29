using UnityEngine;

public enum FeedSourceMode
{
    CameraX,
    DemoVideo
}



[DefaultExecutionOrder(50)]
public class CameraFeedRenderBridge : MonoBehaviour
{
    [Header("Frame Source")]
    [SerializeField] private MonoBehaviour frameSourceBehaviour;

    [Header("Texture Options")]
    [SerializeField] private bool flipY = true;

    [Header("Feed Source")]
    [SerializeField] private FeedSourceMode feedSourceMode = FeedSourceMode.CameraX;

    [Header("Demo Video Input")]
    [SerializeField] private VideoFeedToRenderFeature videoFeed;

    private ICameraFrameSource frameSource;

    private void Awake()
    {
        frameSource = frameSourceBehaviour as ICameraFrameSource;

        if (frameSource == null)
        {
            Debug.LogError("frameSourceBehaviour는 ICameraFrameSource를 구현해야 합니다.");
            enabled = false;
        }
    }
    private void LateUpdate()
    {
        if (frameSource == null || !frameSource.HasFrame || frameSource.FrameTexture == null)
        {
            Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 0f);
            return;
        }

        Shader.SetGlobalTexture(CameraFeedShaderIds.CameraFeedTex, frameSource.FrameTexture);
        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 1f);

        float aspect = frameSource.FrameHeight > 0
            ? (float)frameSource.FrameWidth / frameSource.FrameHeight
            : 1f;

        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAspect, aspect);

        Vector4 st = flipY
            ? new Vector4(1f, -1f, 0f, 1f)
            : new Vector4(1f, 1f, 0f, 0f);

        Shader.SetGlobalVector(CameraFeedShaderIds.CameraFeedST, st);
    }
}