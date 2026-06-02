using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class VideoFeedToRenderFeature : MonoBehaviour, ICameraFrameSource
{
    [Header("Video Player")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RenderTexture videoRenderTexture;

    [Header("Video Source")]
    [SerializeField] private bool useStreamingAssets = true;
    [SerializeField] private string videoFileName = "demo_input.mp4";
    [SerializeField] private VideoClip videoClip;

    [Header("RenderFeature Input")]
    [SerializeField] private string cameraFeedTextureName = "_CameraFeedTex";

    [Header("Options")]
    [SerializeField] private bool controlVideoPlayer = false;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool setGlobalTextureEveryFrame = true;

    public Texture InputTexture => videoRenderTexture;
    public RenderTexture InputRenderTexture => videoRenderTexture;
    public VideoPlayer Player => videoPlayer;
    public bool IsReady { get; private set; }

    public Texture FrameTexture => videoRenderTexture;
    public int FrameWidth => videoRenderTexture != null ? videoRenderTexture.width : 0;
    public int FrameHeight => videoRenderTexture != null ? videoRenderTexture.height : 0;
    public bool HasFrame => IsReady && videoRenderTexture != null && videoRenderTexture.width > 16 && videoRenderTexture.height > 16;

    private int cameraFeedTextureId;

    private IEnumerator Start()
    {
        cameraFeedTextureId = Shader.PropertyToID(cameraFeedTextureName);

        if (videoPlayer == null)
        {
            Debug.LogError("[VideoFeed] VideoPlayer is not assigned.");
            yield break;
        }

        if (videoRenderTexture == null)
        {
            Debug.LogError("[VideoFeed] videoRenderTexture is not assigned.");
            yield break;
        }

        if (controlVideoPlayer)
        {
            SetupVideoPlayer();

            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared)
                yield return null;

            if (playOnStart)
            {
                videoPlayer.Play();
            }
        }

        IsReady = true;

        ApplyGlobalCameraFeedTexture();
    }

    private void Update()
    {
        if (!IsReady)
            return;

        if (setGlobalTextureEveryFrame)
        {
            ApplyGlobalCameraFeedTexture();
        }
    }

    private void SetupVideoPlayer()
    {
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loop;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;

        if (useStreamingAssets)
        {
            string path = Path.Combine(Application.streamingAssetsPath, videoFileName);
            videoPlayer.url = path;
            videoPlayer.source = VideoSource.Url;
        }
        else
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
        }
    }

    private void ApplyGlobalCameraFeedTexture()
    {
        Shader.SetGlobalTexture(cameraFeedTextureId, videoRenderTexture);
        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 1f);
    }
}
