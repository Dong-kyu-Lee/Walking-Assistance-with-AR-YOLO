using UnityEngine;

public enum VisualAssistMode
{
    Normal,
    MildContrast,
    HighContrast,
    StrongContrast,
    GrayscaleHighContrast
}

public class CameraFeedViewModeController : MonoBehaviour
{
    [Header("Screen Shrink Settings")]
    [Range(0.2f, 1.0f)]
    [SerializeField] private float viewScale = 1.0f;

    [SerializeField] private Vector2 viewCenter = new Vector2(0.5f, 0.5f);
    [SerializeField] private Color backgroundColor = Color.black;
    private static readonly float[] ScreenSizeValues = { 1.0f, 0.5f, 0.25f, 0.2f };
    public int ScreenSizeIndex { get; private set; } = 0;

    [Header("High Contrast Settings")]
    [SerializeField] private float contrast = 1.3f;
    [SerializeField] private float brightness = 0.0f;
    [SerializeField] private float saturation = 1.0f;
    [SerializeField] private float grayscaleAmount = 0.0f;

    public int HighContrastIndex { get; private set; } = 1;

    [Header("Detection Overlay")]
    [SerializeField] private RectTransform detectionOverlayRoot;

    private void Start()
    {
        ApplyShaderSettings();
        ApplyDetectionOverlayRect();
    }

    /*private void Update()
    {
        // 테스트 중 Inspector 값 변경을 바로 반영하고 싶으면 유지
        ApplyShaderSettings();
        ApplyDetectionOverlayRect();
    }*/

    public void SetScreenSize(bool reduceScreenSize)
    {
        if (reduceScreenSize)
        {
            ScreenSizeIndex = Mathf.Min(ScreenSizeIndex + 1, ScreenSizeValues.Length - 1);
        }
        else
        {
            ScreenSizeIndex = Mathf.Max(ScreenSizeIndex - 1, 0);
        }

        viewScale = ScreenSizeValues[ScreenSizeIndex];
        ApplyShaderSettings();
        ApplyDetectionOverlayRect();
    }

    public void SetHighContrast(bool increaseContrast)
    {
        if (increaseContrast)
        {
            HighContrastIndex = Mathf.Min(HighContrastIndex + 1, System.Enum.GetValues(typeof(VisualAssistMode)).Length - 1);
        }
        else
        {
            HighContrastIndex = Mathf.Max(HighContrastIndex - 1, 0);
        }
        SetVisualAssistMode((VisualAssistMode)HighContrastIndex);
    }

    public void SetVisualAssistMode(VisualAssistMode mode)
    {
        switch (mode)
        {
            case VisualAssistMode.Normal:
                contrast = 1.0f;
                brightness = 0.0f;
                saturation = 1.0f;
                grayscaleAmount = 0.0f;
                break;

            case VisualAssistMode.MildContrast:
                contrast = 1.3f;
                brightness = 0.0f;
                saturation = 1.0f;
                grayscaleAmount = 0.0f;
                break;

            case VisualAssistMode.HighContrast:
                contrast = 1.6f;
                brightness = 0.0f;
                saturation = 1.1f;
                grayscaleAmount = 0.0f;
                break;

            case VisualAssistMode.StrongContrast:
                contrast = 2.0f;
                brightness = -0.05f;
                saturation = 1.0f;
                grayscaleAmount = 0.0f;
                break;

            case VisualAssistMode.GrayscaleHighContrast:
                contrast = 1.7f;
                brightness = 0.0f;
                saturation = 0.0f;
                grayscaleAmount = 1.0f;
                break;
        }

        ApplyShaderSettings();
    }

    private void ApplyShaderSettings()
    {
        Shader.SetGlobalFloat(CameraFeedShaderIds.ViewScale, viewScale);
        Shader.SetGlobalVector(
            CameraFeedShaderIds.ViewCenter,
            new Vector4(viewCenter.x, viewCenter.y, 0f, 0f)
        );
        Shader.SetGlobalColor(CameraFeedShaderIds.BackgroundColor, backgroundColor);

        Shader.SetGlobalFloat(CameraFeedShaderIds.Contrast, contrast);
        Shader.SetGlobalFloat(CameraFeedShaderIds.Brightness, brightness);
        Shader.SetGlobalFloat(CameraFeedShaderIds.Saturation, saturation);
        Shader.SetGlobalFloat(CameraFeedShaderIds.GrayscaleAmount, grayscaleAmount);
    }

    private void ApplyDetectionOverlayRect()
    {
        if (detectionOverlayRoot == null)
            return;

        float scale = Mathf.Clamp(viewScale, 0.2f, 1.0f);

        Vector2 halfSize = new Vector2(scale * 0.5f, scale * 0.5f);
        Vector2 min = viewCenter - halfSize;
        Vector2 max = viewCenter + halfSize;

        detectionOverlayRoot.anchorMin = min;
        detectionOverlayRoot.anchorMax = max;
        detectionOverlayRoot.offsetMin = Vector2.zero;
        detectionOverlayRoot.offsetMax = Vector2.zero;
        detectionOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
    }
}
