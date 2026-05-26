using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    // loadingUI : 로딩중일때 띄우는 화면
    [SerializeField] private GameObject loadingUI;
    // settingUI : 2D에서 출력되는 모든 화면 오브젝트를 담고 있음.
    [SerializeField] private GameObject settingUI;
    // initialSettingUI : 초기 설정 화면 ( 카메라 높이 )
    [SerializeField] private GameObject initialSettingUI;
    // regularSettingUI : 초기 설정 이후 일반 설정 화면 ( 줌 / 고대비 필터 등 )
    [SerializeField] private GameObject regularSettingUI;
    // cameraHeightInputField : 카메라 높이 입력 필드 요소
    [SerializeField] private TMP_InputField cameraHeightInputField;
    // initialSettingUIExitBtn : 초기 설정 화면에서 나가기 버튼  ( 앱을 처음 실행할 때는 보이지 않도록 설정하고, 그 이후부터는 보이도록 설정)
    [SerializeField] private GameObject initialSettingUIExitBtn;


    [SerializeField] private Button zoomBtn;
    [SerializeField] private Button screenSizeBtn;
    [SerializeField] private Button highContrastBtn;
    [SerializeField] private Button xrRenderBtn;
    [SerializeField] private Button initSettingBtn;

    [Header("실행 요소")]
    [SerializeField] private AppEntryManager appEntrymanager;
    [SerializeField] private VolumeKeyReceiver volumeKeyReceiver;
    [SerializeField] private AndroidTTS androidTTS;

    private void Start()
    {
        settingUI.SetActive(true);
        loadingUI.SetActive(true);
        initialSettingUI.SetActive(false);
        regularSettingUI.SetActive(false);
        volumeKeyReceiver.IsVolumeKeyInputEnabled = false;
        initialSettingUIExitBtn.SetActive(false);

        zoomBtn.onClick.AddListener(() => OnClickSettingTypeBtn(SettingType.Zoom));
        screenSizeBtn.onClick.AddListener(() => OnClickSettingTypeBtn(SettingType.ScreenSize));
        highContrastBtn.onClick.AddListener(() => OnClickSettingTypeBtn(SettingType.HighContrast));
        xrRenderBtn.onClick.AddListener(() => OnClickSettingTypeBtn(SettingType.XRRendering));
        initSettingBtn.onClick.AddListener(() => OnClickSettingTypeBtn(SettingType.InitSetting));
    }
    
    private void OnClickSettingTypeBtn(SettingType type)
    {
        volumeKeyReceiver.currentSettingType = type;
        string typeName = volumeKeyReceiver.GetCurrentModeName(type);
        androidTTS.Speak(typeName, true);
    }
    public void SetLoadingUI(bool isLoading)
    {
        loadingUI.SetActive(isLoading);
        volumeKeyReceiver.IsVolumeKeyInputEnabled = !isLoading;
    }

    public void OpenInitialSetting(bool isInitialConfigured, bool isFirst = false)
    {
        initialSettingUI.SetActive(!isInitialConfigured);
        regularSettingUI.SetActive(isInitialConfigured);
        //volumeKeyReceiver.IsVolumeKeyInputEnabled = isInitialConfigured;

        if (!isFirst && !initialSettingUIExitBtn.activeSelf)
        {
            initialSettingUIExitBtn.SetActive(true);
        }
    }

    public void OnSetupCompleted()
    {
        appEntrymanager.OnSetupCompleted(cameraHeightInputField.text);
    }

    public void OpenSettingUI()
    {
        settingUI.SetActive(true);
        regularSettingUI.SetActive(true);
        initialSettingUI.SetActive(false);
        EventSystem.current.SetSelectedGameObject(initSettingBtn.gameObject);
    }

    public void ShowSelectedBtnEffect(SettingType settingType)
    {
        switch(settingType)
        {
            case SettingType.Zoom:
                EventSystem.current.SetSelectedGameObject(zoomBtn.gameObject);
                break;
            case SettingType.ScreenSize:
                EventSystem.current.SetSelectedGameObject(screenSizeBtn.gameObject);
                break;
            case SettingType.HighContrast:
                EventSystem.current.SetSelectedGameObject(highContrastBtn.gameObject);
                break;
            case SettingType.XRRendering:
                EventSystem.current.SetSelectedGameObject(xrRenderBtn.gameObject);
                break;
            case SettingType.InitSetting:
                EventSystem.current.SetSelectedGameObject(initSettingBtn.gameObject);
                break;
        }
    }

    public void OnClickBeginXRRendering()
    {
        settingUI.SetActive(false);
        appEntrymanager.OnClickXRRendering();
    }

    public void OnClickExitXRRendering()
    {
        settingUI.SetActive(true);
        appEntrymanager.OnClickExitXR();

    }
}
