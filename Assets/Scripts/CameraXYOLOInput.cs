using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

public class CameraXYOLOInput : MonoBehaviour, ICameraFrameSource
{
    [Header("YOLO Reference")]
    public RunYOLO yoloProcessor;
    public bool IsOutlineMode { get; private set; }

    [Header("Permission")]
    [SerializeField] private CameraPermissionRequester permissionRequester;

    [Header("Debug Display")]
    [SerializeField] private bool useRawImagePreview = false;
    public RawImage displayImage;

    [Header("Optimization Settings")]
    [Range(0.1f, 2.0f)]
    [Tooltip("YOLO 추론 주기 (초 단위). 예: 0.2초 = 5FPS")]
    public float inferenceIntervalSeconds = 0.5f;
    [Tooltip("카메라 프레임 업데이트 주기 값")]
    public float CameraUpdateInterval = 1f / 30f;
    private float cameraUpdateTimer = 0f;

    [Header("Zoom UI")]
    [SerializeField] private Slider slider;

    private AndroidJavaObject cameraController;
    private Texture2D cameraTexture;
    private AspectRatioFitter aspectFitter;

    private float zoomValue = 0.0f;
    private bool isProcessing = false;
    private float inferenceTimer = 0f;

    private int lastWidth = 0;
    private int lastHeight = 0;

    public Texture FrameTexture => cameraTexture;
    public int FrameWidth => cameraTexture != null ? cameraTexture.width : 0;
    public int FrameHeight => cameraTexture != null ? cameraTexture.height : 0;
    public bool HasFrame => cameraTexture != null && cameraTexture.width > 16 && cameraTexture.height > 16;

    private static readonly ProfilerMarker MarkerGetFrameData =
    new ProfilerMarker("CameraX.GetLatestFrameData");

    private static readonly ProfilerMarker MarkerTextureUpload =
        new ProfilerMarker("CameraX.TextureUpload");

    private static readonly ProfilerMarker MarkerYOLO =
        new ProfilerMarker("YOLO.ExecuteML");

    private IEnumerator Start()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            //Debug.Log("Android 플랫폼이 아니므로 CameraX를 시작하지 않습니다.");
            yield break;
        }

        if (permissionRequester == null)
        {
            permissionRequester = GetComponent<CameraPermissionRequester>();

            if (permissionRequester == null)
            {
                permissionRequester = gameObject.AddComponent<CameraPermissionRequester>();
            }
        }

        yield return permissionRequester.RequestCameraPermission();

        if (!permissionRequester.IsGranted)
        {
            //Debug.LogWarning("카메라 권한이 승인되지 않아 CameraX를 시작하지 않습니다.");
            yield break;
        }

        InitializeCameraX();
    }

    private void InitializeCameraX()
    {
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            string pluginName = "com.example.unitycamerax.CameraXController";
            cameraController = new AndroidJavaObject(pluginName, currentActivity);

            cameraController.Call("startCamera");

            //Debug.Log("CameraX 초기화 명령 전송 완료");
        }
        catch (System.Exception e)
        {
            //Debug.LogError($"CameraX 초기화 실패: {e.Message}");
        }
    }

    private void Update()
    {
        if (cameraController == null)
            return;

        cameraUpdateTimer += Time.deltaTime;

        if (cameraUpdateTimer >= CameraUpdateInterval)
        {
            cameraUpdateTimer = 0f;
            UpdateCameraFeed();
        }

        //UpdateCameraFeed();

        if (IsOutlineMode == false) return;
        if (yoloProcessor != null && yoloProcessor.IsModelLoaded && cameraTexture != null)
        {
            inferenceTimer += Time.deltaTime;

            if (inferenceTimer >= inferenceIntervalSeconds && !isProcessing)
            {
                inferenceTimer = 0f;
                StartCoroutine(RunInference());
            }
        }
    }

    private void UpdateCameraFeed()
    {

        int width = cameraController.Call<int>("getFrameWidth");
        int height = cameraController.Call<int>("getFrameHeight");

        if (width <= 0 || height <= 0)
            return;

        byte[] frameData;
        using (MarkerGetFrameData.Auto())
        {
            frameData = cameraController.Call<byte[]>("getLatestFrameData");
        }

        if (frameData == null || frameData.Length <= 0)
            return;

        if (cameraTexture == null || cameraTexture.width != width || cameraTexture.height != height)
        {
            cameraTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        }
        using (MarkerTextureUpload.Auto())
        {
            cameraTexture.LoadRawTextureData(frameData);
            cameraTexture.Apply();
        }
        
        // Render Feature를 사용할 경우 RawImage 출력은 필수가 아님.
        // 디버그용 미리보기로만 사용.

        if (width != lastWidth || height != lastHeight)
        {
            lastWidth = width;
            lastHeight = height;

            if (useRawImagePreview && displayImage != null)
            {
                displayImage.texture = cameraTexture;
                displayImage.uvRect = new Rect(0, 1, 1, -1);

                if (aspectFitter == null)
                {
                    aspectFitter = displayImage.GetComponent<AspectRatioFitter>();

                    if (aspectFitter == null)
                    {
                        aspectFitter = displayImage.gameObject.AddComponent<AspectRatioFitter>();
                        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    }
                }

                aspectFitter.aspectRatio = (float)width / height;
            }
        }

        
    }

    private IEnumerator RunInference()
    {
        isProcessing = true;
        MarkerYOLO.Begin();
        yield return StartCoroutine(yoloProcessor.ExecuteML(cameraTexture));
        MarkerYOLO.End();
        isProcessing = false;
    }

    public void SetLinearZoom()
    {
        if (slider == null)
        {
            //Debug.LogWarning("Slider가 연결되지 않았습니다.");
            return;
        }

        zoomValue = slider.value;

        if (Application.platform == RuntimePlatform.Android)
        {
            if (cameraController != null)
            {
                cameraController.Call("setLinearZoom", zoomValue);
                //Debug.Log($"Linear Zoom 값 전송: {zoomValue}");
            }
        }
        else
        {
            //Debug.Log("Android 플랫폼이 아닙니다.");
        }
    }

    public void SetZoomRatio(float ratio)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (cameraController != null)
            {
                cameraController.Call("setZoomRatio", ratio);
                //Debug.Log($"Zoom Ratio 값 전송: {ratio}");
            }
        }
        else
        {
            //Debug.Log("Android 플랫폼이 아닙니다.");
        }
    }

    private void OnDestroy()
    {
        if (cameraController != null)
        {
            cameraController.Call("stopCamera");
            cameraController.Dispose();
            cameraController = null;

            //Debug.Log("CameraX 종료: 카메라 권한을 반환합니다.");
        }

        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }
    }

    public void SetOulineMode()
    {
        IsOutlineMode = !IsOutlineMode;

        if (IsOutlineMode == false)
        {
            yoloProcessor.ClearAnnotations();
        }
    }
}