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
    public float CameraUpdateInterval = 1f / 60f;
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

    [Header("FOV Auto Calibration")]
    [SerializeField] private bool useAutoFovCorrection = true;
    [SerializeField] private float hmdHorizontalFov = 74.2f;

    [Header("FOV User Adjustment")]
    [SerializeField] private float userAdjustZoom = 1.0f;
    [SerializeField] private float userAdjustStep = 0.05f;
    [SerializeField] private float minUserAdjustZoom = 0.9f;
    [SerializeField] private float maxUserAdjustZoom = 1.3f;

    private float cameraHorizontalFov = -1f;
    private float appliedZoomRatio = 1f;

    private const string UserAdjustZoomKey = "UserAdjustZoom";

    [Header("Aspect Ratio Correction")]
    [SerializeField] private bool useAspectCorrection = true;

    private Vector2 aspectCropScale = Vector2.one;

    public Vector2 AspectCropScale => aspectCropScale;


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

            if (useAutoFovCorrection)
            {
                userAdjustZoom = PlayerPrefs.GetFloat(UserAdjustZoomKey, 1.0f);
                StartCoroutine(ApplyFovCorrectionWhenReady());
            }

            //Debug.Log("CameraX 초기화 명령 전송 완료");
        }
        catch (System.Exception e)
        {
            //Debug.LogError($"CameraX 초기화 실패: {e.Message}");
        }
    }

    private IEnumerator ApplyFovCorrectionWhenReady()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            bool isBound = false;

            try
            {
                isBound = cameraController.Call<bool>("isCameraBound");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"isCameraBound 호출 실패: {e.Message}");
            }

            if (isBound)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        bool finalBound = cameraController.Call<bool>("isCameraBound");

        if (!finalBound)
        {
            Debug.LogWarning("CameraX 바인딩 실패 또는 시간 초과. FOV 보정을 적용하지 않습니다.");
            yield break;
        }

        ApplyFovCorrection();
    }

    public void ApplyFovCorrection()
    {
        if (Application.platform != RuntimePlatform.Android)
            return;

        if (cameraController == null)
            return;

        try
        {
            cameraHorizontalFov =
                cameraController.Call<float>("getBoundCameraHorizontalFov");

            appliedZoomRatio =
                cameraController.Call<float>(
                    "applyAutoFovZoomWithUserAdjust",
                    hmdHorizontalFov
                );

            Debug.Log(
                $"FOV 보정 적용: CameraFOV={cameraHorizontalFov:F2}, " +
                $"HMDFOV={hmdHorizontalFov:F2}, " +
                $"UserAdjust={userAdjustZoom:F2}, " +
                $"AppliedZoom={appliedZoomRatio:F2}"
            );
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"FOV 보정 실패: {e.Message}");
        }
    }

    private void UpdateAspectCorrection(int width, int height)
    {
        if (!useAspectCorrection)
        {
            aspectCropScale = Vector2.one;
            Shader.SetGlobalVector("_CameraAspectCrop", new Vector4(1f, 1f, 0f, 0f));
            return;
        }

        float cameraAspect = (float)width / height;

        // Cardboard/XR 한쪽 눈 화면 비율 추정
        float eyeAspect = (Screen.width * 0.5f) / Screen.height;

        float cropX = 1f;
        float cropY = 1f;

        if (cameraAspect > eyeAspect)
        {
            // 카메라 영상이 한쪽 눈 화면보다 더 넓음
            // 좌우를 중앙 기준으로 크롭
            cropX = eyeAspect / cameraAspect;
        }
        else
        {
            // 카메라 영상이 한쪽 눈 화면보다 더 좁음
            // 상하를 중앙 기준으로 크롭
            cropY = cameraAspect / eyeAspect;
        }

        aspectCropScale = new Vector2(cropX, cropY);

        Shader.SetGlobalVector(
            "_CameraAspectCrop",
            new Vector4(cropX, cropY, 0f, 0f)
        );

        Debug.Log(
            $"AspectCorrection: CameraAspect={cameraAspect:F3}, " +
            $"EyeAspect={eyeAspect:F3}, CropX={cropX:F3}, CropY={cropY:F3}"
        );
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

            UpdateAspectCorrection(width, height);

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
                appliedZoomRatio = cameraController.Call<float>("setZoomRatio", ratio);
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