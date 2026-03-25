using UnityEngine;
using UnityEngine.UI;

public class CameraXManager : MonoBehaviour
{
    private AndroidJavaObject cameraController;
    private float zoomValue = 0.0f; // 슬라이더에서 조정할 줌 값 (0.0 ~ 1.0)
    [SerializeField] Slider slider; // 슬라이더 UI 연결

    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            // 1. 유니티의 현재 화면(Activity) 정보 가져오기
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // 2. 안드로이드 플러그인 연결하기
            string pluginName = "com.example.unitycamerax.CameraXController";
            cameraController = new AndroidJavaObject(pluginName, currentActivity);

            // 3. 카메라 켜기 명령 내리기
            cameraController.Call("startCamera");
            Debug.Log("CameraX 초기화 명령 전송 완료!");
        }
        else Debug.Log("Android 플랫폼이 아닙니다.");
    }

    // 슬라이더 줌 기능 (0.0 ~ 1.0)
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

    // 갤럭시 S23+ 다중 렌즈 전환 기능 (0.6: 초광각, 1.0: 일반, 3.0: 망원)
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
}
