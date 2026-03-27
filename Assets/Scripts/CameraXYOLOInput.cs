using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraXYOLOInput : MonoBehaviour
{
    [Header("YOLO Reference")]
    public RunYOLO yoloProcessor; // 기존 RunYOLO.cs 연결

    [Header("Optimization Settings")]
    [Range(1, 60)]
    public int inferenceInterval = 5;
    
    [SerializeField] private Slider slider;
    private AndroidJavaObject cameraController;
    [SerializeField] private Texture2D cameraTexture;
    private float zoomValue = 0.0f; // 슬라이더에서 조정할 줌 값 (0.0 ~ 1.0)
    private bool isProcessing = false;

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
        // YOLO 모델이 로드되었고, 이전 추론이 끝났다면 다음 프레임 가져오기 시작
        if (!isProcessing && yoloProcessor.IsModelLoaded && cameraController != null)
        {
            StartCoroutine(FetchFrameAndRunInference());
        }
    }

    private IEnumerator FetchFrameAndRunInference()
    {
        isProcessing = true;
        Debug.Log("카메라 프레임 처리 시작");

        // 1. 네이티브 플러그인에서 현재 카메라 해상도 가져오기
        int width = cameraController.Call<int>("getFrameWidth");
        int height = cameraController.Call<int>("getFrameHeight");

        if (width > 0 && height > 0)
        {
            // 2. 픽셀 데이터(RGBA)를 byte 배열로 가져오기
            byte[] frameData = cameraController.Call<byte[]>("getLatestFrameData");

            if (frameData != null && frameData.Length > 0)
            {
                // 3. 텍스처 초기화 (해상도가 바뀌거나 처음 생성할 때)
                if (cameraTexture == null || cameraTexture.width != width || cameraTexture.height != height)
                {
                    // CameraX의 RGBA_8888 포맷과 유니티의 RGBA32는 완벽하게 호환됩니다.
                    cameraTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                }

                // 4. 데이터를 텍스처에 덮어씌우고 GPU로 전송
                cameraTexture.LoadRawTextureData(frameData);
                cameraTexture.Apply();

                // 5. RunYOLO.cs로 텍스처를 넘겨서 추론 실행 및 화면 출력
                yield return StartCoroutine(yoloProcessor.ExecuteML(cameraTexture));
            }
        }

        // 6. 지정된 프레임 간격만큼 대기 (부하 조절)
        for (int i = 0; i < inferenceInterval; i++)
        {
            yield return null;
        }

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