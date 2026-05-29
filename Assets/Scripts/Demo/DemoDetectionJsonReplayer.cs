using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class DemoDetectionJsonReplayer : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RenderTexture videoRenderTexture;

    [Header("JSON")]
    [SerializeField] private TextAsset detectionJsonFile;

    [Header("Polygon Renderer")]
    [SerializeField] private PolygonOverlayTextureRenderer polygonRenderer;

    [Header("Options")]
    [SerializeField] private bool useFrameReadyEvent = true;
    [SerializeField] private bool setCameraFeedTexture = true;
    [SerializeField] private bool showOnlyHighPriority = true;
    [SerializeField] private int minPriority = 70;
    [SerializeField] private int maxObjectCount = 3;

    private DemoDetectionJson detectionJson;
    private Dictionary<int, DemoFrameJson> frameMap;

    private long lastFrame = -1;
    private DemoFrameJson lastValidFrameData;

    private void Start()
    {
        LoadJson();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.waitForFirstFrame = true;

        if (setCameraFeedTexture)
        {
            Shader.SetGlobalTexture("_CameraFeedTex", videoRenderTexture);
        }

        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnPrepared;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.frameReady -= OnFrameReady;
            videoPlayer.prepareCompleted -= OnPrepared;
        }
    }

    private void OnPrepared(VideoPlayer vp)
    {
        if (useFrameReadyEvent)
        {
            vp.sendFrameReadyEvents = true;
            vp.frameReady += OnFrameReady;
        }

        vp.Play();
    }

    private void Update()
    {
        if (setCameraFeedTexture && videoRenderTexture != null)
        {
            Shader.SetGlobalTexture("_CameraFeedTex", videoRenderTexture);
        }

        if (useFrameReadyEvent)
            return;

        long currentFrame = videoPlayer.frame;

        if (currentFrame < 0)
            return;

        if (currentFrame == lastFrame)
            return;

        lastFrame = currentFrame;
        DrawFrame((int)currentFrame);
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        DrawFrame((int)frameIdx);
    }

    private void LoadJson()
    {
        if (detectionJsonFile == null)
        {
            Debug.LogError("detectionJsonFile이 연결되지 않았습니다.");
            return;
        }

        detectionJson = JsonUtility.FromJson<DemoDetectionJson>(detectionJsonFile.text);

        frameMap = new Dictionary<int, DemoFrameJson>();

        foreach (var frame in detectionJson.frames)
        {
            frameMap[frame.frame] = frame;
        }

        Debug.Log($"JSON 로드 완료: frames={frameMap.Count}, video={detectionJson.videoFile}");
    }

    private void DrawFrame(int frameIndex)
    {
        if (polygonRenderer == null)
            return;

        if (frameMap == null)
            return;

        DemoFrameJson frameData;

        if (frameMap.TryGetValue(frameIndex, out frameData))
        {
            lastValidFrameData = frameData;
        }
        else
        {
            // processEveryNthFrame을 1보다 크게 했을 경우,
            // 중간 프레임에서 폴리곤이 깜빡이지 않게 마지막 결과 유지
            frameData = lastValidFrameData;
        }

        polygonRenderer.ClearAll();

        if (frameData == null || frameData.objects == null)
            return;

        List<DemoObjectJson> objects = new List<DemoObjectJson>(frameData.objects);

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
                points[p] = new Vector2(
                    obj.polygon[p].x,
                    obj.polygon[p].y
                );
            }

            polygonRenderer.ShowPolygon(i, points, Color.green);
        }
    }
}