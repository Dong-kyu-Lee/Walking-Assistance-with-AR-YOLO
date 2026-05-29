using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class VideoFeedToRenderFeature : MonoBehaviour
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
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool setGlobalTextureEveryFrame = true;

    public Texture InputTexture => videoRenderTexture;
    public RenderTexture InputRenderTexture => videoRenderTexture;
    public VideoPlayer Player => videoPlayer;
    public bool IsReady { get; private set; }

    private int cameraFeedTextureId;

    private IEnumerator Start()
    {
        cameraFeedTextureId = Shader.PropertyToID(cameraFeedTextureName);

        if (videoPlayer == null)
        {
            Debug.LogError("[VideoFeed] VideoPlayer가 연결되지 않았습니다.");
            yield break;
        }

        if (videoRenderTexture == null)
        {
            Debug.LogError("[VideoFeed] videoRenderTexture가 연결되지 않았습니다.");
            yield break;
        }

        SetupVideoPlayer();

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
            yield return null;

        IsReady = true;

        // 첫 프레임을 RenderTexture에 올리기 위해 재생 시작
        if (playOnStart)
            videoPlayer.Play();
        else
            videoPlayer.Pause();

        // 핵심: RenderFeature가 읽는 전역 텍스처에 비디오 RT 연결
        Shader.SetGlobalTexture(cameraFeedTextureId, videoRenderTexture);
    }

    private void Update()
    {
        if (!IsReady) return;

        if (setGlobalTextureEveryFrame)
        {
            Shader.SetGlobalTexture(cameraFeedTextureId, videoRenderTexture);
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

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android StreamingAssets는 apk 내부 경로가 될 수 있음.
            // VideoPlayer.url은 보통 Application.streamingAssetsPath 기반 경로로 사용 가능.
            videoPlayer.url = path;
#else
            videoPlayer.url = path;
#endif
            videoPlayer.source = VideoSource.Url;
        }
        else
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
        }
    }
}