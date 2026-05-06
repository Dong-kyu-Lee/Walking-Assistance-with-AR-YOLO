using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum SettingType
{
    Zoom,
    ScreenSize,
    HighContrast,
    Outline
}

public class VolumeKeyReceiver : MonoBehaviour
{
    [Header("CameraX 줌 설정")]
    [SerializeField] CameraXYOLOInput cameraX;
    [SerializeField] float zoomRatioStep = 0.5f;
    [SerializeField] float minZoomRatio = 0.6f;
    [SerializeField] float maxZoomRatio = 5.0f;

    [Header("시각보조 옵션")]
    [SerializeField] CameraFeedViewModeController cameraFeedViewModeController;

    [Header("디버그용 Text")]
    [SerializeField] private TextMeshProUGUI currentSettingTypeText;

    private float currentZoomRatio = 1.0f;
    private SettingType currentSettingType = SettingType.Zoom;

    void Start()
    {
        if (cameraX == null)
            cameraX = FindFirstObjectByType<CameraXYOLOInput>();

        currentSettingTypeText.text = "Screen Size";

#if UNITY_ANDROID && !UNITY_EDITOR
        // Window.Callback 방식으로 볼륨 키 가로채기 초기화
        new AndroidJavaClass("com.unity.template.ar_mobile.VolumeKeyPlugin")
            .CallStatic("init");
#endif
    }

    // ────────────────────────────────────────────
    // Java UnitySendMessage 콜백
    // 이 GameObject의 이름이 반드시 "VolumeKeyReceiver" 이어야 함
    // ────────────────────────────────────────────

    public void OnVolumeUp(string msg)
    {
        Debug.Log("Time held (ms): " + msg);
        ZoomIn();
    }

    public void OnVolumeDown(string msg)
    {
        Debug.Log("Time held (ms): " + msg);
        ZoomOut();
    }

    public void OnVolumeUpLong(string msg)
    {
        Debug.Log("[볼륨UP 길게]");
        // SwitchToScene("ARScene");
    }

    public void OnVolumeDownLong(string msg)
    {
        Debug.Log("[볼륨DOWN 길게]");
        // SwitchToScene("MainScene");
    }

    // ────────────────────────────────────────────
    // 에디터 테스트용 (PageUp/PageDown 키로 확인)
    // ────────────────────────────────────────────

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.PageUp))   ZoomIn();
        if (Input.GetKeyDown(KeyCode.PageDown)) ZoomOut();
    }
#endif

    // ────────────────────────────────────────────
    // 줌 로직
    // ────────────────────────────────────────────

    private void ZoomIn()
    {
        if (cameraX == null) return;
        currentZoomRatio = Mathf.Clamp(currentZoomRatio + zoomRatioStep, minZoomRatio, maxZoomRatio);
        cameraX.SetZoomRatio(currentZoomRatio);
        Debug.Log($"[볼륨UP] 줌인 → ZoomRatio: {currentZoomRatio}");
    }

    private void ZoomOut()
    {
        if (cameraX == null) return;
        currentZoomRatio = Mathf.Clamp(currentZoomRatio - zoomRatioStep, minZoomRatio, maxZoomRatio);
        cameraX.SetZoomRatio(currentZoomRatio);
        Debug.Log($"[볼륨DOWN] 줌아웃 → ZoomRatio: {currentZoomRatio}");
    }

    // 화면 Scale 감소 메소드
    private void ReduceScreenSize()
    {
        cameraFeedViewModeController.SetScreenSize(true);
    }

    // 화면 Scale 증가 메소드
    private void EnlargeScreenSize()
    {
        cameraFeedViewModeController.SetScreenSize(false);
    } 

    private void SetHighContrastMode()
    {
        cameraFeedViewModeController.SetHighContrast(true);
    }

    private void SetLowContrastMode()
    {
        cameraFeedViewModeController.SetHighContrast(false);
    }
}
