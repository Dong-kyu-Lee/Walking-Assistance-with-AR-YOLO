using UnityEngine;

public static class CameraFeedShaderIds
{
    public static readonly int CameraFeedTex = Shader.PropertyToID("_CameraFeedTex");
    public static readonly int CameraFeedAvailable = Shader.PropertyToID("_CameraFeedAvailable");
    public static readonly int CameraFeedAspect = Shader.PropertyToID("_CameraFeedAspect");
    public static readonly int CameraFeedST = Shader.PropertyToID("_CameraFeed_ST");
}