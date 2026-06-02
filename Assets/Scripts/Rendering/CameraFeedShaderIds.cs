using UnityEngine;

public static class CameraFeedShaderIds
{
    public static readonly int CameraFeedTex = Shader.PropertyToID("_CameraFeedTex");
    public static readonly int CameraFeedAvailable = Shader.PropertyToID("_CameraFeedAvailable");
    public static readonly int CameraFeedAspect = Shader.PropertyToID("_CameraFeedAspect");
    public static readonly int CameraFeedST = Shader.PropertyToID("_CameraFeed_ST");
    public static readonly int CameraAspectCrop = Shader.PropertyToID("_CameraAspectCrop");
    public static readonly int UseXRUv = Shader.PropertyToID("_UseXRUv");
    public static readonly int XRCameraFeedFlipY = Shader.PropertyToID("_XRCameraFeedFlipY");

    public static readonly int ViewScale = Shader.PropertyToID("_ViewScale");
    public static readonly int ViewCenter = Shader.PropertyToID("_ViewCenter");
    public static readonly int BackgroundColor = Shader.PropertyToID("_BackgroundColor");

    public static readonly int Contrast = Shader.PropertyToID("_Contrast");
    public static readonly int Brightness = Shader.PropertyToID("_Brightness");
    public static readonly int Saturation = Shader.PropertyToID("_Saturation");
    public static readonly int GrayscaleAmount = Shader.PropertyToID("_GrayscaleAmount");
}