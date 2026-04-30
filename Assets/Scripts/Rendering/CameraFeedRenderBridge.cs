using UnityEngine;

[DefaultExecutionOrder(50)]
public class CameraFeedRenderBridge : MonoBehaviour
{
    [Header("Frame Source")]
    [SerializeField] private MonoBehaviour frameSourceBehaviour;

    [Header("Texture Options")]
    [SerializeField] private bool flipY = true;

    private ICameraFrameSource frameSource;

    private void Awake()
    {
        frameSource = frameSourceBehaviour as ICameraFrameSource;

        if (frameSource == null)
        {
            Debug.LogError("frameSourceBehaviour는 ICameraFrameSource를 구현해야 합니다.");
        }
    }

    private void LateUpdate()
    {
        if (frameSource == null || !frameSource.HasFrame)
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

        // RawImage에서 쓰던 uvRect = new Rect(0, 1, 1, -1)와 같은 역할
        if (flipY)
        {
            Shader.SetGlobalVector(CameraFeedShaderIds.CameraFeedST, new Vector4(1f, -1f, 0f, 1f));
        }
        else
        {
            Shader.SetGlobalVector(CameraFeedShaderIds.CameraFeedST, new Vector4(1f, 1f, 0f, 0f));
        }
    }
}