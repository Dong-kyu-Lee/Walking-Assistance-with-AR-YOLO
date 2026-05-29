using System.Collections;
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

    private DemoDetectionJson detectionJson;

    private bool frameReady;
    private long readyFrame;

    private IEnumerator Start()
    {
        yield return ExportRoutine();
    }

    private IEnumerator ExportRoutine()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayer가 연결되지 않았습니다.");
            yield break;
        }

        if (videoRenderTexture == null)
        {
            Debug.LogError("videoRenderTexture가 연결되지 않았습니다.");
            yield break;
        }

        if (runYOLO == null)
        {
            Debug.LogError("RunYOLO가 연결되지 않았습니다.");
            yield break;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.frameReady += OnFrameReady;

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
            yield return null;

        videoPlayer.Pause();

        int totalFrames = (int)videoPlayer.frameCount;

        if (maxFrameCount > 0)
            totalFrames = Mathf.Min(totalFrames, maxFrameCount);

        detectionJson = new DemoDetectionJson
        {
            videoFile = GetVideoFileName(),
            videoWidth = videoRenderTexture.width,
            videoHeight = videoRenderTexture.height,
            fps = videoPlayer.frameRate,
            frameCount = totalFrames
        };

        Debug.Log($"JSON 생성 시작: frames={totalFrames}, fps={videoPlayer.frameRate}");

        int step = Mathf.Max(1, processEveryNthFrame);

        for (int frameIndex = 0; frameIndex < totalFrames; frameIndex += step)
        {
            yield return SeekFrame(frameIndex);

            if (setCameraFeedTexture)
            {
                Shader.SetGlobalTexture("_CameraFeedTex", videoRenderTexture);
            }

            DemoFrameJson frameData = new DemoFrameJson
            {
                frame = frameIndex
            };

            bool inferenceDone = false;

            yield return StartCoroutine(runYOLO.ExecuteMLForJson(
                videoRenderTexture,
                objects =>
                {
                    frameData.objects = objects;
                    inferenceDone = true;
                },
                Quaternion.identity
            ));

            while (!inferenceDone)
                yield return null;

            detectionJson.frames.Add(frameData);

            if (frameIndex % 30 == 0)
            {
                Debug.Log($"JSON 생성 중: {frameIndex}/{totalFrames}, objects={frameData.objects.Count}");
            }
        }

        SaveJson();

        videoPlayer.frameReady -= OnFrameReady;

        Debug.Log("JSON 생성 완료");
    }

    private IEnumerator SeekFrame(int targetFrame)
    {
        frameReady = false;
        readyFrame = -1;

        videoPlayer.Pause();
        videoPlayer.frame = targetFrame;

        // VideoPlayer가 해당 frame을 실제 RenderTexture에 올리도록 잠깐 재생
        videoPlayer.Play();

        int guard = 0;

        while ((!frameReady || readyFrame != targetFrame) && guard < 120)
        {
            guard++;
            yield return null;
        }

        videoPlayer.Pause();

        // RenderTexture 반영 안정화
        yield return null;
        yield return new WaitForEndOfFrame();
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        frameReady = true;
        readyFrame = frameIdx;
    }

    private void SaveJson()
    {
        string json = JsonUtility.ToJson(detectionJson, true);

        string dir = Path.Combine(Application.dataPath, "StreamingAssets", folderName);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, outputFileName);

        File.WriteAllText(path, json);

        Debug.Log($"JSON 저장 위치: {path}");
    }

    private string GetVideoFileName()
    {
        if (videoPlayer.clip != null)
            return videoPlayer.clip.name;

        if (!string.IsNullOrEmpty(videoPlayer.url))
            return Path.GetFileName(videoPlayer.url);

        return "unknown_video";
    }
}