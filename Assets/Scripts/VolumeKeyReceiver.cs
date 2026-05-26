using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum SettingType
{
    Zoom,
    ScreenSize,
    HighContrast,
    XRRendering,
    InitSetting
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

    [SerializeField] private AppEntryManager appEntryManager;
    [SerializeField] private SettingsUI settingsUI;

    public bool IsVolumeKeyInputEnabled = false;

    private float currentZoomRatio = 1.0f;
    public SettingType currentSettingType = SettingType.Zoom;

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
        if (IsVolumeKeyInputEnabled == false) return;
        Debug.Log("Time held (ms): " + msg);
        ExecutionVolumeUpMode();
    }

    public void OnVolumeDown(string msg)
    {
        if (IsVolumeKeyInputEnabled == false) return;
        Debug.Log("Time held (ms): " + msg);
        ExecutionVolumeDownMode();
    }

    public void OnVolumeUpLong(string msg)
    {
        if (IsVolumeKeyInputEnabled == false) return;
        Debug.Log("[볼륨UP 길게]");
        // SwitchToScene("ARScene");
        GetNextSetting();
    }

    public void OnVolumeDownLong(string msg)
    {
        if (IsVolumeKeyInputEnabled == false) return;
        Debug.Log("[볼륨DOWN 길게]");
        // SwitchToScene("MainScene");
        GetBeforeSetting();
    }

    public void GetBeforeSetting()
    {
        Debug.Log($"Current Setting Type : {currentSettingType}");
        currentSettingType = GetBeforeSettingType(currentSettingType);
        currentSettingTypeText.text = GetCurrentModeName(currentSettingType);
        GetVoice(GetCurrentModeName(currentSettingType));
        settingsUI.ShowSelectedBtnEffect(currentSettingType);
    }

    public void GetNextSetting()
    {
        Debug.Log($"Current Setting Type : {currentSettingType}");
        currentSettingType = GetNextSettingType(currentSettingType);
        currentSettingTypeText.text = GetCurrentModeName(currentSettingType);
        GetVoice(GetCurrentModeName(currentSettingType));
        settingsUI.ShowSelectedBtnEffect(currentSettingType);
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

    private SettingType GetNextSettingType(SettingType type)
    {
        return type switch
        {
            SettingType.Zoom => SettingType.ScreenSize,
            SettingType.ScreenSize => SettingType.HighContrast,
            SettingType.HighContrast => SettingType.XRRendering,
            SettingType.XRRendering => SettingType.InitSetting,
            SettingType.InitSetting => SettingType.Zoom,
            _ => SettingType.Zoom
        };
    }
    private SettingType GetBeforeSettingType(SettingType type)
    {
        return type switch
        {
            SettingType.Zoom => SettingType.InitSetting,
            SettingType.InitSetting => SettingType.XRRendering,
            SettingType.XRRendering => SettingType.HighContrast,
            SettingType.HighContrast => SettingType.ScreenSize,
            SettingType.ScreenSize => SettingType.Zoom,
            _ => SettingType.Zoom
        };
    }

    private void ExecutionVolumeUpMode()
    {
        switch (currentSettingType)
        {
            case SettingType.Zoom:
                ZoomIn();
                break;
            case SettingType.ScreenSize:
                EnlargeScreenSize();
                break;
            case SettingType.HighContrast:
                SetHighContrastMode();
                break;
            case SettingType.XRRendering:
                settingsUI.OnClickBeginXRRendering();
                break;
            case SettingType.InitSetting:
                settingsUI.OpenInitialSetting(false);
                break;
        }
    }

    private void ExecutionVolumeDownMode()
    {
        switch (currentSettingType)
        {
            case SettingType.Zoom:
                ZoomOut();
                break;
            case SettingType.ScreenSize:
                ReduceScreenSize();
                break;
            case SettingType.HighContrast:
                SetLowContrastMode();
                break;
            case SettingType.XRRendering:
                settingsUI.OnClickExitXRRendering();
                break;
            case SettingType.InitSetting:
                settingsUI.OpenInitialSetting(true);
                break;
        }
    }

    public string GetCurrentModeName(SettingType type)
    {
        return type switch
        {
            SettingType.Zoom => "줌 옵션",
            SettingType.ScreenSize => "화면 크기 축소 옵션",
            SettingType.HighContrast => "고대비 필터 옵션",
            SettingType.XRRendering => "양안 렌더링 옵션",
            SettingType.InitSetting => "초기 설정 옵션",
            _ => "알 수 없는 옵션"
        };
    }
    
    [SerializeField] private AndroidTTS androidTTS;

    public void GetVoice(string sentence)
    {
        androidTTS.Speak(sentence, true);
    }
}
