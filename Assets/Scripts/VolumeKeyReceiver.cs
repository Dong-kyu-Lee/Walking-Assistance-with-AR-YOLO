using UnityEngine;
using UnityEngine.SceneManagement;

public class VolumeKeyReceiver : MonoBehaviour
{
    [Header("CameraX 줌 설정")]
    [SerializeField] CameraXYOLOInput cameraX;
    [SerializeField] float zoomRatioStep = 0.5f;
    [SerializeField] float minZoomRatio = 0.6f;
    [SerializeField] float maxZoomRatio = 5.0f;

    private float currentZoomRatio = 1.0f;

    void Start()
    {
        if (cameraX == null)
            cameraX = FindFirstObjectByType<CameraXYOLOInput>();

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
        ZoomIn();
    }

    public void OnVolumeDown(string msg)
    {
        ZoomOut();
    }

    // 길게 누르기 → ARScene으로 씬 전환 (시각보조 모드 변경)
    public void OnVolumeUpLong(string msg)
    {
        SwitchToScene("ARScene");
    }

    public void OnVolumeDownLong(string msg)
    {
        SwitchToScene("MainScene");
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

    private void SwitchToScene(string targetScene)
    {
        Debug.Log($"[볼륨 길게] → {targetScene} 씬 전환");
        SceneManager.LoadScene(targetScene);
    }
}
