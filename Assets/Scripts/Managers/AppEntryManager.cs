using System.Collections;
using TMPro;
using UnityEngine;

public class AppEntryManager : MonoBehaviour
{
    [SerializeField] private XRRuntimeController xrRuntimeController;
    [SerializeField] private GameObject setupUI;
    [SerializeField] private GameObject loadingUI;
    [SerializeField] private TMP_InputField cameraHeightInputField;
    [SerializeField] private AndroidTTS androidTTS;
    [SerializeField] private GroundPlaneDistanceEstimator groundPlaneDistanceEstimator;
    [SerializeField] private VolumeKeyReceiver volumeKeyReceiver;
    [SerializeField] private GameObject cardboardDeviceParamsController;

    void Start()
    {
        StartCoroutine(Init());
    }

    public IEnumerator Init()
    {
        cardboardDeviceParamsController.SetActive(false);

        loadingUI.SetActive(true);
        setupUI.SetActive(false);
        yield return new WaitForSeconds(0.5f);

        bool isConfigured = PlayerPrefs.GetInt("CameraHeightConfigured", 0) == 1;
        if (isConfigured)
        {
            StartCoroutine(BeginXRFlow());
            androidTTS.Speak("카메라 높이 설정이 완료되었습니다. XR 환경으로 진입합니다.");
        }
        else
        {
            loadingUI.SetActive(false);
            setupUI.SetActive(true);
            androidTTS.Speak("카메라 높이 설정이 완료되지 않았습니다. 설정을 진행해주세요.");
        }
    }

    public void OnSetupCompleted()
    {
        if (float.TryParse(cameraHeightInputField.text, out float results))
        {
            PlayerPrefs.SetFloat("CameraHeightM", results);
            PlayerPrefs.SetInt("CameraHeightConfigured", 1);
            PlayerPrefs.Save();
            groundPlaneDistanceEstimator.cameraHeightMeters = results;
        }
        setupUI.SetActive(false);
        loadingUI.SetActive(true);
        StartCoroutine(BeginXRFlow());
    }

    private IEnumerator BeginXRFlow()
    {
        yield return xrRuntimeController.StartXR();
        loadingUI.SetActive(false);
        // XR 시작 후 보행 보조 기능 활성화 또는 씬 전환
        Debug.Log("XR 시작 완료");
        volumeKeyReceiver.IsVolumeKeyInputEnabled = true;
        cardboardDeviceParamsController.SetActive(true);
    }
}