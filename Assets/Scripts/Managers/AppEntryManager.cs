using System.Collections;
using TMPro;
using UnityEngine;

public class AppEntryManager : MonoBehaviour
{
    [SerializeField] private SettingsUI settingsUI;
    [SerializeField] private XRRuntimeController xrRuntimeController;
    [SerializeField] private AndroidTTS androidTTS;
    [SerializeField] private GroundPlaneDistanceEstimator groundPlaneDistanceEstimator;
    [SerializeField] private VolumeKeyReceiver volumeKeyReceiver;
    [SerializeField] private GameObject cardboardDeviceParamsController;
    [SerializeField] private CameraXYOLOInput cameraXYOLOInput;

    void Start()
    {
        StartCoroutine(Init());
    }

    public IEnumerator Init()
    {
        cardboardDeviceParamsController.SetActive(false);
        cameraXYOLOInput.SetOutlineMode();

        yield return new WaitForSeconds(3f);
        settingsUI.SetLoadingUI(false);

        bool isConfigured = PlayerPrefs.GetInt("CameraHeightConfigured", 0) == 1;
        
        settingsUI.OpenInitialSetting(isConfigured, true);
    }

    public void OnSetupCompleted(string cameraHeight)
    {
        if (float.TryParse(cameraHeight, out float results))
        {
            PlayerPrefs.SetFloat("CameraHeightM", results);
            PlayerPrefs.SetInt("CameraHeightConfigured", 1);
            PlayerPrefs.Save();
            groundPlaneDistanceEstimator.cameraHeightMeters = results;
        }
        settingsUI.OpenInitialSetting(true);
    }

    public void OnClickXRRendering()
    {
        StartCoroutine(BeginXRFlow());
    }

    public void OnClickExitXR()
    {
        StartCoroutine(EndXRFlow());
    }

    private IEnumerator BeginXRFlow()
    {
        yield return xrRuntimeController.StartXR();
        // XR 시작 후 보행 보조 기능 활성화 또는 씬 전환
        Debug.Log("XR 시작 완료");
        cardboardDeviceParamsController.SetActive(true);

        yield break;
    }

    private IEnumerator EndXRFlow()
    {
        xrRuntimeController.StopXR();
        cardboardDeviceParamsController.SetActive(false);
        settingsUI.OpenSettingUI();
        yield break;
    }
}