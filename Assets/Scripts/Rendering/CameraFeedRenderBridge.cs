using UnityEngine;

[DefaultExecutionOrder(50)]
public class CameraFeedRenderBridge : MonoBehaviour
{
    [Header("Frame Source")]
    [SerializeField] private MonoBehaviour frameSourceBehaviour;

    [Header("Texture Options")]
    [SerializeField] private bool flipY = true;

    [Header("View Options")]
    [SerializeField, Range(0.2f, 1.0f)] private float viewScale = 1f;
    [SerializeField] private Vector2 viewCenter = new Vector2(0.5f, 0.5f);
    [SerializeField] private Color backgroundColor = Color.black;

    private ICameraFrameSource frameSource;

    private void Awake()
    {
        frameSource = frameSourceBehaviour as ICameraFrameSource;

        if (frameSource == null)
        {
            Debug.LogError("frameSourceBehaviour는 ICameraFrameSource를 구현해야 합니다.");
        }
    }

    private void Start()
    {
        Shader.SetGlobalFloat(CameraFeedShaderIds.ViewScale, viewScale);
        Shader.SetGlobalVector(CameraFeedShaderIds.ViewCenter, viewCenter);
        Shader.SetGlobalColor(CameraFeedShaderIds.BackgroundColor, backgroundColor);

        ApplyDetectionOverlayRect();
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

    [SerializeField] private RectTransform detectionOverlayRoot;

    private void ApplyDetectionOverlayRect()
    {
        if (detectionOverlayRoot == null)
            return;

        float scale = viewScale;

        Vector2 halfSize = new Vector2(scale * 0.5f, scale * 0.5f);
        Vector2 min = viewCenter - halfSize;
        Vector2 max = viewCenter + halfSize;

        detectionOverlayRoot.anchorMin = min;
        detectionOverlayRoot.anchorMax = max;
        detectionOverlayRoot.offsetMin = Vector2.zero;
        detectionOverlayRoot.offsetMax = Vector2.zero;
        detectionOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
    }

    public void SetScale(float scale)
    {
        viewScale = scale;
        Shader.SetGlobalFloat(CameraFeedShaderIds.ViewScale, viewScale);
        ApplyDetectionOverlayRect();
    }
    public void SetViewCenter(Vector2 center)
    {
        viewCenter = center;
        Shader.SetGlobalVector(CameraFeedShaderIds.ViewCenter, viewCenter);
        ApplyDetectionOverlayRect();
    }
    public void SetColor(Color color)
    {
        backgroundColor = color;
        Shader.SetGlobalColor(CameraFeedShaderIds.BackgroundColor, backgroundColor);
        ApplyDetectionOverlayRect();
    }
}