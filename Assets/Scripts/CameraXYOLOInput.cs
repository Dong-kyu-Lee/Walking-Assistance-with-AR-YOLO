using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraXYOLOInput : MonoBehaviour
{
    [Header("YOLO Reference")]
    public RunYOLO yoloProcessor; // 기존 RunYOLO.cs 연결

    [Header("Display")]
    public RawImage displayImage; // 카메라 프레임을 보여줄 UI 요소

    [Header("Optimization Settings")]
    [Range(0.1f, 2.0f)]
    [Tooltip("YOLO 추론 주기 (초 단위). 예: 0.2초 = 5FPS")]
    public float inferenceIntervalSeconds = 0.2f;

    [SerializeField] private Slider slider;
    private AndroidJavaObject cameraController;
    private Texture2D cameraTexture;
    private AspectRatioFitter aspectFitter;
    private float zoomValue = 0.0f; // 슬라이더에서 조정할 줌 값 (0.0 ~ 1.0)
    private bool isProcessing = false;
    private float inferenceTimer = 0f;

    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            // 플러그인 연결 및 카메라 켜기
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            string pluginName = "com.example.unitycamerax.CameraXController";
            cameraController = new AndroidJavaObject(pluginName, currentActivity);
            cameraController.Call("startCamera");
            Debug.Log("CameraX 초기화 명령 전송 완료");
        }
    }

    void Update()
    {
        if (cameraController != null)
        {
            // 1. 카메라는 매 프레임 가져와서 화면을 부드럽게 갱신합니다.
            UpdateCameraFeed();

            // 2. YOLO 추론은 타이머를 이용해 별도의 주기로 실행합니다.
            if (yoloProcessor.IsModelLoaded && cameraTexture != null)
            {
                inferenceTimer += Time.deltaTime;
                if (inferenceTimer >= inferenceIntervalSeconds && !isProcessing)
                {
                    inferenceTimer = 0f;
                    StartCoroutine(RunInference());
                }
            }
        }
    }

    private void UpdateCameraFeed()
    {
        int width = cameraController.Call<int>("getFrameWidth");
        int height = cameraController.Call<int>("getFrameHeight");

        if (width > 0 && height > 0)
        {
            Debug.Log($"카메라 프레임 크기: {width}x{height}");
            byte[] frameData = cameraController.Call<byte[]>("getLatestFrameData");

            if (frameData != null && frameData.Length > 0)
            {
                if (cameraTexture == null || cameraTexture.width != width || cameraTexture.height != height)
                {
                    cameraTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                }

                cameraTexture.LoadRawTextureData(frameData);
                cameraTexture.Apply();

                // 원본 텍스처를 화면에 즉시 렌더링
                if (displayImage != null)
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
    }

    private IEnumerator RunInference()
    {
        isProcessing = true;
        yield return StartCoroutine(yoloProcessor.ExecuteML(cameraTexture));
        isProcessing = false;
    }

    public void SetLinearZoom()
    {
        zoomValue = slider.value; // 슬라이더에서 현재 값 가져오기
        if (Application.platform == RuntimePlatform.Android)
        {
            if (cameraController != null)
            {
                cameraController.Call("setLinearZoom", zoomValue);
                Debug.Log($"Linear Zoom 값 전송: {zoomValue}");
            }
        }
        else Debug.Log("Android 플랫폼이 아닙니다.");
    }

    public void SetZoomRatio(float ratio)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (cameraController != null)
            {
                cameraController.Call("setZoomRatio", ratio);
                Debug.Log($"Zoom Ratio 값 전송: {ratio}");
            }
        }
        else Debug.Log("Android 플랫폼이 아닙니다.");
    }

    void OnDestroy()
    {
        // [매우 중요] 객체 탐지 씬이 종료될 때 반드시 카메라를 꺼주어야 AR 씬에서 카메라를 쓸 수 있습니다!
        if (cameraController != null)
        {
            cameraController.Call("stopCamera");
            Debug.Log("CameraX 종료: 카메라 권한을 반환합니다.");
        }
    }
}