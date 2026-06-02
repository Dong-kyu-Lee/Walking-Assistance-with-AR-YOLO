using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class DemoDetectionJsonReplayer : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RenderTexture videoRenderTexture;

    [Header("JSON")]
    [SerializeField] private TextAsset detectionJsonFile;
    [SerializeField] private bool loadJsonFromStreamingAssets = true;
    [SerializeField] private string streamingAssetsJsonPath = "Demo/demo_detections.json";

    [Header("Polygon Renderer")]
    [SerializeField] private PolygonOverlayTextureRenderer polygonRenderer;

    [Header("Options")]
    [SerializeField] private bool useFrameReadyEvent = true;
    [SerializeField] private bool setCameraFeedTexture = true;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool showOnlyHighPriority = false;
    [SerializeField] private int minPriority = 70;
    [SerializeField] private int maxObjectCount = 3;
    [SerializeField] private bool holdLastDetectionOnEmptyFrames = true;
    [SerializeField] private int maxHoldFrameGap = 10;
    [SerializeField] private bool logLifecycle = true;
    [SerializeField] private bool logDrawStats = true;

    private DemoDetectionJson detectionJson;
    private Dictionary<int, DemoFrameJson> frameMap;
    private long lastFrame = -1;
    private DemoFrameJson lastValidFrameData;
    private int lastValidFrameIndex = -1;
    private float lastPlaybackLogTime = -1f;
    private bool playbackRequested;

    private void Start()
    {
        if (logLifecycle)
        {
            Debug.Log("[DemoReplayer] Start.");
        }

        if (!ValidateReferences())
            return;

        LoadJson();

        if (frameMap == null)
            return;

        SetupVideoPlayerForReplay();
        ApplyCameraFeedTexture();

        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.frameReady -= OnFrameReady;
            videoPlayer.prepareCompleted -= OnPrepared;
        }
    }

    private bool ValidateReferences()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("[DemoReplayer] VideoPlayer is not assigned.");
            return false;
        }

        if (videoRenderTexture == null)
        {
            Debug.LogError("[DemoReplayer] videoRenderTexture is not assigned.");
            return false;
        }

        if (!loadJsonFromStreamingAssets && detectionJsonFile == null)
        {
            Debug.LogError("[DemoReplayer] detectionJsonFile is not assigned.");
            return false;
        }

        if (polygonRenderer == null)
        {
            Debug.LogError("[DemoReplayer] PolygonOverlayTextureRenderer is not assigned.");
            return false;
        }

        return true;
    }

    private void SetupVideoPlayerForReplay()
    {
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loop;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.waitForFirstFrame = true;
    }

    private void OnPrepared(VideoPlayer vp)
    {
        if (logLifecycle)
        {
            Debug.Log($"[DemoReplayer] Video prepared. frameCount={vp.frameCount}, frameRate={vp.frameRate}, playOnStart={playOnStart}");
        }

        if (useFrameReadyEvent)
        {
            vp.sendFrameReadyEvents = true;
            vp.frameReady -= OnFrameReady;
            vp.frameReady += OnFrameReady;
        }

        if (playOnStart)
            StartPlayback();
        else
            vp.Pause();
    }

    private void Update()
    {
        ApplyCameraFeedTexture();
        EnsurePlaybackStarted();

        long currentFrame = videoPlayer.frame;

        LogPlaybackProgress(currentFrame);

        if (currentFrame < 0 || currentFrame == lastFrame)
            return;

        DrawFrame((int)currentFrame);
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        DrawFrame((int)frameIdx);
    }

    private void LoadJson()
    {
        string json = LoadJsonText();

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("[DemoReplayer] Detection JSON is empty or not found.");
            return;
        }

        detectionJson = JsonUtility.FromJson<DemoDetectionJson>(json);

        if (detectionJson == null || detectionJson.frames == null)
        {
            Debug.LogError("[DemoReplayer] Failed to load detection JSON.");
            return;
        }

        frameMap = new Dictionary<int, DemoFrameJson>();
        int framesWithObjects = 0;

        foreach (DemoFrameJson frame in detectionJson.frames)
        {
            frameMap[frame.frame] = frame;

            if (frame.objects != null && frame.objects.Count > 0)
            {
                framesWithObjects++;
            }
        }

        Debug.Log($"[DemoReplayer] JSON loaded. frames={frameMap.Count}, framesWithObjects={framesWithObjects}, video={detectionJson.videoFile}");
    }

    private string LoadJsonText()
    {
        if (!loadJsonFromStreamingAssets)
        {
            return detectionJsonFile != null ? detectionJsonFile.text : string.Empty;
        }

        string path = Path.Combine(Application.streamingAssetsPath, streamingAssetsJsonPath);

        if (!File.Exists(path))
        {
            Debug.LogError($"[DemoReplayer] Detection JSON file not found: {path}");
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    private void ApplyCameraFeedTexture()
    {
        if (!setCameraFeedTexture || videoRenderTexture == null)
            return;

        Shader.SetGlobalTexture(CameraFeedShaderIds.CameraFeedTex, videoRenderTexture);
        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 1f);
    }

    private void DrawFrame(int frameIndex)
    {
        if (frameIndex == lastFrame)
            return;

        lastFrame = frameIndex;

        if (polygonRenderer == null || frameMap == null)
            return;

        DemoFrameJson frameData;

        if (frameMap.TryGetValue(frameIndex, out frameData))
        {
            if (frameData.objects != null && frameData.objects.Count > 0)
            {
                lastValidFrameData = frameData;
                lastValidFrameIndex = frameIndex;
            }
        }
        else
        {
            frameData = lastValidFrameData;
        }

        polygonRenderer.ClearAll();

        if (frameData == null || frameData.objects == null || frameData.objects.Count == 0)
        {
            if (holdLastDetectionOnEmptyFrames &&
                lastValidFrameData != null &&
                lastValidFrameData.objects != null &&
                frameIndex - lastValidFrameIndex <= maxHoldFrameGap)
            {
                frameData = lastValidFrameData;
            }
            else
            {
                if (logDrawStats && frameIndex % 30 == 0)
                {
                    Debug.Log($"[DemoReplayer] frame={frameIndex}, rawObjects=0, drawnObjects=0");
                }

                return;
            }
        }

        List<DemoObjectJson> objects = new List<DemoObjectJson>(frameData.objects);
        int rawObjectCount = objects.Count;

        if (showOnlyHighPriority)
        {
            objects.RemoveAll(o => o.priority < minPriority);
        }

        objects.Sort((a, b) =>
        {
            int priorityCompare = b.priority.CompareTo(a.priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return b.score.CompareTo(a.score);
        });

        if (maxObjectCount > 0 && objects.Count > maxObjectCount)
        {
            objects = objects.GetRange(0, maxObjectCount);
        }

        for (int i = 0; i < objects.Count; i++)
        {
            DemoObjectJson obj = objects[i];

            if (obj.polygon == null || obj.polygon.Count < 3)
                continue;

            Vector2[] points = new Vector2[obj.polygon.Count];

            for (int p = 0; p < obj.polygon.Count; p++)
            {
                points[p] = new Vector2(obj.polygon[p].x, obj.polygon[p].y);
            }

            polygonRenderer.ShowPolygon(i, points, Color.green);
        }

        if (logDrawStats && objects.Count > 0)
        {
            Debug.Log($"[DemoReplayer] frame={frameIndex}, rawObjects={rawObjectCount}, drawnObjects={objects.Count}");
        }
    }

    private void LogPlaybackProgress(long currentFrame)
    {
        if (!logDrawStats || Time.unscaledTime - lastPlaybackLogTime < 1f)
            return;

        lastPlaybackLogTime = Time.unscaledTime;
        Debug.Log($"[DemoReplayer] playback isPlaying={videoPlayer.isPlaying}, frame={currentFrame}, time={videoPlayer.time:F2}");
    }

    private void StartPlayback()
    {
        playbackRequested = true;
        videoPlayer.Play();
    }

    private void EnsurePlaybackStarted()
    {
        if (!playOnStart || !playbackRequested || videoPlayer.isPlaying)
            return;

        if (!videoPlayer.isPrepared)
            return;

        videoPlayer.Play();
    }
}
