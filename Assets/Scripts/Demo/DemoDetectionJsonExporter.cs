using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class DemoDetectionJsonExporter : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RenderTexture videoRenderTexture;

    [Header("YOLO")]
    [SerializeField] private RunYOLO runYOLO;

    [Header("Output")]
    [SerializeField] private string folderName = "Demo";
    [SerializeField] private string outputFileName = "demo_detections.json";

    [Header("Options")]
    [SerializeField] private int processEveryNthFrame = 1;
    [SerializeField] private int maxFrameCount = -1;
    [SerializeField] private bool setCameraFeedTexture = true;
    [SerializeField] private bool exportOnStart = false;
    [SerializeField] private bool pauseOtherPlaybackDuringExport = true;

    private DemoDetectionJson detectionJson;
    private bool frameReady;
    private long readyFrame;
    private bool isExporting;
    private readonly List<DemoDetectionJsonReplayer> disabledReplayers = new();
    private readonly List<VideoPlayer> pausedVideoPlayers = new();

    private IEnumerator Start()
    {
        if (exportOnStart)
        {
            yield return ExportRoutine();
        }
    }

    public void Export()
    {
        if (isExporting)
        {
            Debug.LogWarning("[DemoExporter] Export is already running.");
            return;
        }

        StartCoroutine(ExportRoutine());
    }

    private IEnumerator ExportRoutine()
    {
        if (!ValidateReferences())
            yield break;

        isExporting = true;
        BeginExportIsolation();
        SetupVideoPlayerForExport();

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
            yield return null;

        videoPlayer.Pause();

        int totalFrames = GetTotalFrameCount();
        int step = Mathf.Max(1, processEveryNthFrame);

        detectionJson = new DemoDetectionJson
        {
            videoFile = GetVideoFileName(),
            videoWidth = videoRenderTexture.width,
            videoHeight = videoRenderTexture.height,
            fps = videoPlayer.frameRate,
            frameCount = totalFrames
        };

        Debug.Log($"[DemoExporter] JSON export started. frames={totalFrames}, fps={videoPlayer.frameRate}");

        for (int frameIndex = 0; frameIndex < totalFrames; frameIndex += step)
        {
            yield return SeekFrame(frameIndex);
            ApplyCameraFeedTexture();

            DemoFrameJson frameData = new DemoFrameJson
            {
                frame = frameIndex
            };

            yield return StartCoroutine(runYOLO.ExecuteMLForJson(
                videoRenderTexture,
                objects => frameData.objects = objects,
                Quaternion.identity
            ));

            detectionJson.frames.Add(frameData);

            if (frameIndex % 30 == 0)
            {
                Debug.Log($"[DemoExporter] frame={frameIndex}/{totalFrames}, objects={frameData.objects.Count}");
            }
        }

        SaveJson();
        videoPlayer.frameReady -= OnFrameReady;
        EndExportIsolation();
        isExporting = false;
        Debug.Log("[DemoExporter] JSON export completed.");
    }

    private bool ValidateReferences()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("[DemoExporter] VideoPlayer is not assigned.");
            return false;
        }

        if (videoRenderTexture == null)
        {
            Debug.LogError("[DemoExporter] videoRenderTexture is not assigned.");
            return false;
        }

        if (runYOLO == null)
        {
            Debug.LogError("[DemoExporter] RunYOLO is not assigned.");
            return false;
        }

        return true;
    }

    private void SetupVideoPlayerForExport()
    {
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.frameReady -= OnFrameReady;
        videoPlayer.frameReady += OnFrameReady;
    }

    private int GetTotalFrameCount()
    {
        int totalFrames = videoPlayer.frameCount > 0
            ? (int)videoPlayer.frameCount
            : Mathf.CeilToInt((float)(videoPlayer.length * videoPlayer.frameRate));

        if (maxFrameCount > 0)
            totalFrames = Mathf.Min(totalFrames, maxFrameCount);

        return Mathf.Max(0, totalFrames);
    }

    private IEnumerator SeekFrame(int targetFrame)
    {
        frameReady = false;
        readyFrame = -1;

        videoPlayer.Pause();
        videoPlayer.frame = targetFrame;
        videoPlayer.Play();

        int guard = 0;

        while ((!frameReady || readyFrame < targetFrame) &&
               videoPlayer.frame < targetFrame &&
               guard < 120)
        {
            guard++;
            yield return null;
        }

        videoPlayer.Pause();

        yield return null;
        yield return new WaitForEndOfFrame();
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        frameReady = true;
        readyFrame = frameIdx;
    }

    private void ApplyCameraFeedTexture()
    {
        if (!setCameraFeedTexture)
            return;

        Shader.SetGlobalTexture(CameraFeedShaderIds.CameraFeedTex, videoRenderTexture);
        Shader.SetGlobalFloat(CameraFeedShaderIds.CameraFeedAvailable, 1f);
    }

    private void SaveJson()
    {
        string json = JsonUtility.ToJson(detectionJson, true);
        string dir = Path.Combine(Application.dataPath, "StreamingAssets", folderName);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, outputFileName);
        File.WriteAllText(path, json);

        Debug.Log($"[DemoExporter] JSON saved: {path}");
    }

    private string GetVideoFileName()
    {
        if (videoPlayer.clip != null)
            return videoPlayer.clip.name;

        if (!string.IsNullOrEmpty(videoPlayer.url))
            return Path.GetFileName(videoPlayer.url);

        return "unknown_video";
    }

    private void BeginExportIsolation()
    {
        disabledReplayers.Clear();
        pausedVideoPlayers.Clear();

        if (!pauseOtherPlaybackDuringExport)
            return;

        DemoDetectionJsonReplayer[] replayers =
            FindObjectsByType<DemoDetectionJsonReplayer>(FindObjectsSortMode.None);

        foreach (DemoDetectionJsonReplayer replayer in replayers)
        {
            if (replayer == null || !replayer.enabled)
                continue;

            replayer.enabled = false;
            disabledReplayers.Add(replayer);
        }

        VideoPlayer[] players = FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None);

        foreach (VideoPlayer player in players)
        {
            if (player == null || player == videoPlayer)
                continue;

            if (player.targetTexture != videoRenderTexture)
                continue;

            if (player.isPlaying)
            {
                player.Pause();
                pausedVideoPlayers.Add(player);
            }
        }
    }

    private void EndExportIsolation()
    {
        foreach (DemoDetectionJsonReplayer replayer in disabledReplayers)
        {
            if (replayer != null)
            {
                replayer.enabled = true;
            }
        }

        disabledReplayers.Clear();
        pausedVideoPlayers.Clear();
    }
}
