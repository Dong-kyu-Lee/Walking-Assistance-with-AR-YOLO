using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

public class CameraXYOLOInput : MonoBehaviour
{
    [Header("YOLO Reference")]
    [SerializeField] private RunYOLO yoloProcessor;

    [Header("Display")]
    [SerializeField] private RawImage displayImage;

    [Header("UI")]
    [SerializeField] private Slider slider;

    [Header("Optimization Settings")]
    [Range(0.1f, 2.0f)]
    [Tooltip("YOLO 추론 주기 (초 단위). 예: 0.2초 = 5FPS")]
    [SerializeField] private float inferenceIntervalSeconds = 0.2f;

    private AndroidJavaObject cameraController;
    private Texture2D cameraTexture;
    private AspectRatioFitter aspectRatioFitter;

    private float zoomValue = 0.0f;
    private bool isProcessing = false;
    private float inferenceTimer = 0f;

    private bool permissionResolved = false;
    private bool cameraPermissionGranted = false;
    private bool isCameraStarted = false;
    private bool isInitializing = false;

    private const string PluginClassName = "com.example.unitycamerax.CameraXController";

    [Header("Background Output")]
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private string backgroundTexturePropertyName = "_Step2BackgroundTex";
    [SerializeField] private bool outputToBackground = true;

    [Header("Preview RenderTexture")]
    [SerializeField] private bool usePreviewRenderTexture = true;
    [SerializeField] private int previewRTWidth = 1280;
    [SerializeField] private int previewRTHeight = 720;
    [SerializeField] private Material previewCopyMaterial; // 없으면 null로 둬도 됨

    [Header("Debug View")]
    [SerializeField] private bool outputToRawImage = false;

    private int backgroundTexturePropertyId;
    private RenderTexture previewRT;

    private void Awake()
    {
        backgroundTexturePropertyId = Shader.PropertyToID(backgroundTexturePropertyName);
    }

    private IEnumerator Start()
    {
        if (displayImage != null)
            aspectRatioFitter = displayImage.GetComponent<AspectRatioFitter>();

        yield return StartCoroutine(InitializeRoutine());
    }

    private IEnumerator InitializeRoutine()
    {
        if (isInitializing)
            yield break;

        isInitializing = true;

        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.LogWarning("[CameraXYOLOInput] Android 전용 코드입니다.");
            isInitializing = false;
            yield break;
        }

        yield return StartCoroutine(RequestCameraPermissionRoutine());

        if (!cameraPermissionGranted)
        {
            Debug.LogError("[CameraXYOLOInput] 카메라 권한이 없어 초기화를 중단합니다.");
            isInitializing = false;
            yield break;
        }

        // 권한 승인 직후 / 씬 진입 직후 CameraX 초기화 안정화용
        yield return null;
        yield return new WaitForSeconds(0.2f);

        InitializeCameraController();
        StartCamera();

        isInitializing = false;
    }

    private IEnumerator RequestCameraPermissionRoutine()
    {
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            cameraPermissionGranted = true;
            permissionResolved = true;
            yield break;
        }

        permissionResolved = false;
        cameraPermissionGranted = false;

        PermissionCallbacks callbacks = new PermissionCallbacks();

        callbacks.PermissionGranted += permission =>
        {
            if (permission == Permission.Camera)
            {
                cameraPermissionGranted = true;
                permissionResolved = true;
                Debug.Log("[CameraXYOLOInput] 카메라 권한 승인됨");
            }
        };

        callbacks.PermissionDenied += permission =>
        {
            if (permission == Permission.Camera)
            {
                cameraPermissionGranted = false;
                permissionResolved = true;
                Debug.LogWarning("[CameraXYOLOInput] 카메라 권한 거부됨");
            }
        };

        callbacks.PermissionDeniedAndDontAskAgain += permission =>
        {
            if (permission == Permission.Camera)
            {
                cameraPermissionGranted = false;
                permissionResolved = true;
                Debug.LogWarning("[CameraXYOLOInput] 카메라 권한 거부됨 (다시 묻지 않음)");
            }
        };

        Permission.RequestUserPermission(Permission.Camera, callbacks);

        float timeout = 10f;
        float timer = 0f;

        while (!permissionResolved && timer < timeout)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!permissionResolved)
        {
            cameraPermissionGranted = Permission.HasUserAuthorizedPermission(Permission.Camera);
            permissionResolved = true;
            Debug.LogWarning("[CameraXYOLOInput] 권한 응답 대기 시간이 초과되어 현재 권한 상태를 다시 확인했습니다.");
        }
    }

    private void InitializeCameraController()
    {
        if (cameraController != null)
            return;

        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            cameraController = new AndroidJavaObject(PluginClassName, currentActivity);

            if (cameraController == null)
            {
                Debug.LogError("[CameraXYOLOInput] CameraXController 생성 실패");
                return;
            }

            Debug.Log("[CameraXYOLOInput] CameraXController 생성 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] CameraXController 초기화 실패: {e}");
        }
    }

    private void StartCamera()
    {
        if (cameraController == null)
        {
            Debug.LogError("[CameraXYOLOInput] cameraController가 null이라 startCamera 호출 불가");
            return;
        }

        if (isCameraStarted)
            return;

        try
        {
            cameraController.Call("startCamera");
            isCameraStarted = true;
            Debug.Log("[CameraXYOLOInput] startCamera 호출 완료");
        }
        catch (System.Exception e)
        {
            isCameraStarted = false;
            Debug.LogError($"[CameraXYOLOInput] startCamera 호출 실패: {e}");
        }
    }

    private void StopCamera()
    {
        if (cameraController == null)
            return;

        if (!isCameraStarted)
            return;

        try
        {
            cameraController.Call("stopCamera");
            isCameraStarted = false;
            Debug.Log("[CameraXYOLOInput] stopCamera 호출 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] stopCamera 호출 실패: {e}");
        }
    }

    private void Update()
    {
        if (Application.platform != RuntimePlatform.Android)
            return;

        if (!cameraPermissionGranted)
            return;

        if (cameraController == null || !isCameraStarted)
            return;

        UpdateCameraFeed();

        if (yoloProcessor != null &&
            yoloProcessor.IsModelLoaded &&
            cameraTexture != null &&
            !isProcessing)
        {
            inferenceTimer += Time.deltaTime;

            if (inferenceTimer >= inferenceIntervalSeconds)
            {
                inferenceTimer = 0f;
                StartCoroutine(RunInference());
            }
        }
    }

    private void UpdateCameraFeed()
    {
        /*int width = 0;
        int height = 0;

        try
        {
            width = cameraController.Call<int>("getFrameWidth");
            height = cameraController.Call<int>("getFrameHeight");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] 프레임 크기 조회 실패: {e}");
            return;
        }

        if (width <= 0 || height <= 0)
            return;

        byte[] frameData = null;

        try
        {
            frameData = cameraController.Call<byte[]>("getLatestFrameData");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] 프레임 데이터 조회 실패: {e}");
            return;
        }

        if (frameData == null || frameData.Length == 0)
            return;

        int expectedLength = width * height * 4; // RGBA32 기준
        if (frameData.Length < expectedLength)
        {
            Debug.LogWarning($"[CameraXYOLOInput] 프레임 데이터 길이가 예상보다 짧습니다. length={frameData.Length}, expected={expectedLength}");
            return;
        }

        if (cameraTexture == null || cameraTexture.width != width || cameraTexture.height != height)
        {
            if (cameraTexture != null)
                Destroy(cameraTexture);

            cameraTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cameraTexture.wrapMode = TextureWrapMode.Clamp;
            cameraTexture.filterMode = FilterMode.Bilinear;
        }

        cameraTexture.LoadRawTextureData(frameData);
        cameraTexture.Apply(false);

        if (displayImage != null)
        {
            displayImage.texture = cameraTexture;

            // Android 카메라 프레임 상하 반전 보정
            displayImage.uvRect = new Rect(0, 1, 1, -1);

            if (aspectRatioFitter != null)
                aspectRatioFitter.aspectRatio = (float)width / height;
        }*/
        int width = 0;
        int height = 0;

        try
        {
            width = cameraController.Call<int>("getFrameWidth");
            height = cameraController.Call<int>("getFrameHeight");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] 프레임 크기 조회 실패: {e}");
            return;
        }

        if (width <= 0 || height <= 0)
            return;

        byte[] frameData = null;

        try
        {
            frameData = cameraController.Call<byte[]>("getLatestFrameData");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] 프레임 데이터 조회 실패: {e}");
            return;
        }

        if (frameData == null || frameData.Length == 0)
            return;

        int expectedLength = width * height * 4; // RGBA32 기준
        if (frameData.Length < expectedLength)
        {
            Debug.LogWarning($"[CameraXYOLOInput] 프레임 데이터 길이가 예상보다 짧습니다. length={frameData.Length}, expected={expectedLength}");
            return;
        }

        if (cameraTexture == null || cameraTexture.width != width || cameraTexture.height != height)
        {
            if (cameraTexture != null)
                Destroy(cameraTexture);

            cameraTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cameraTexture.wrapMode = TextureWrapMode.Clamp;
            cameraTexture.filterMode = FilterMode.Bilinear;
        }

        cameraTexture.LoadRawTextureData(frameData);
        cameraTexture.Apply(false);

        // 1) RenderTexture 준비
        if (usePreviewRenderTexture)
        {
            CreateOrResizePreviewRT(width, height);

            if (previewRT != null)
            {
                if (previewCopyMaterial != null)
                    Graphics.Blit(cameraTexture, previewRT, previewCopyMaterial);
                else
                    Graphics.Blit(cameraTexture, previewRT);
            }
        }

        // 2) 배경 출력
        if (outputToBackground && backgroundMaterial != null)
        {
            if (usePreviewRenderTexture && previewRT != null)
                backgroundMaterial.SetTexture(backgroundTexturePropertyId, previewRT);
            else
                backgroundMaterial.SetTexture(backgroundTexturePropertyId, cameraTexture);
        }

        // 3) 선택적 RawImage 출력
        if (outputToRawImage && displayImage != null)
        {
            displayImage.texture = usePreviewRenderTexture && previewRT != null ? previewRT : cameraTexture;
            displayImage.uvRect = new Rect(0, 1, 1, -1);

            if (aspectRatioFitter != null)
                aspectRatioFitter.aspectRatio = (float)width / height;
        }
    }

    private IEnumerator RunInference()
    {
        if (yoloProcessor == null || cameraTexture == null)
            yield break;

        isProcessing = true;
        yield return StartCoroutine(yoloProcessor.ExecuteML(cameraTexture));
        isProcessing = false;
    }

    public void SetLinearZoom()
    {
        if (slider == null)
        {
            Debug.LogWarning("[CameraXYOLOInput] Slider가 연결되지 않았습니다.");
            return;
        }

        zoomValue = slider.value;

        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.Log("[CameraXYOLOInput] Android 플랫폼이 아닙니다.");
            return;
        }

        if (cameraController == null)
        {
            Debug.LogWarning("[CameraXYOLOInput] cameraController가 null입니다.");
            return;
        }

        try
        {
            cameraController.Call("setLinearZoom", zoomValue);
            Debug.Log($"[CameraXYOLOInput] Linear Zoom 값 전송: {zoomValue}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] setLinearZoom 실패: {e}");
        }
    }

    public void SetZoomRatio(float ratio)
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.Log("[CameraXYOLOInput] Android 플랫폼이 아닙니다.");
            return;
        }

        if (cameraController == null)
        {
            Debug.LogWarning("[CameraXYOLOInput] cameraController가 null입니다.");
            return;
        }

        try
        {
            cameraController.Call("setZoomRatio", ratio);
            Debug.Log($"[CameraXYOLOInput] Zoom Ratio 값 전송: {ratio}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraXYOLOInput] setZoomRatio 실패: {e}");
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (Application.platform != RuntimePlatform.Android)
            return;

        if (!cameraPermissionGranted)
            return;

        if (pause)
        {
            StopCamera();
        }
        else
        {
            if (cameraController == null)
                InitializeCameraController();

            StartCamera();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (Application.platform != RuntimePlatform.Android)
            return;

        if (!cameraPermissionGranted)
            return;

        if (hasFocus)
        {
            if (cameraController == null)
                InitializeCameraController();

            StartCamera();
        }
    }

    private void OnDestroy()
    {
        /*StopCamera();

        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }

        cameraController = null;*/

        StopCamera();

        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }

        if (previewRT != null)
        {
            previewRT.Release();
            Destroy(previewRT);
            previewRT = null;
        }

        cameraController = null;
    }

    private void CreateOrResizePreviewRT(int width, int height)
    {
        if (!usePreviewRenderTexture)
            return;

        int targetWidth = previewRTWidth > 0 ? previewRTWidth : width;
        int targetHeight = previewRTHeight > 0 ? previewRTHeight : height;

        if (previewRT != null &&
            previewRT.width == targetWidth &&
            previewRT.height == targetHeight)
        {
            return;
        }

        if (previewRT != null)
        {
            previewRT.Release();
            Destroy(previewRT);
        }

        previewRT = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        previewRT.useMipMap = false;
        previewRT.autoGenerateMips = false;
        previewRT.wrapMode = TextureWrapMode.Clamp;
        previewRT.filterMode = FilterMode.Bilinear;
        previewRT.Create();

        Debug.Log($"[CameraXYOLOInput] previewRT 생성: {targetWidth}x{targetHeight}");
    }
}