using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

public class XRRuntimeController : MonoBehaviour
{
    public bool IsXRRunning { get; private set; }

    private void Awake()
    {
        IsXRRunning = false;
    }
    public IEnumerator StartXR()
    {
        if (IsXRRunning)
            yield break;

        Debug.Log("Initializing XR...");

        yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

        if (XRGeneralSettings.Instance.Manager.activeLoader == null)
        {
            Debug.LogError("XR 초기화 실패");
            yield break;
        }

        Debug.Log("Starting XR...");
        XRGeneralSettings.Instance.Manager.StartSubsystems();

        IsXRRunning = true;
    }

    public void StopXR()
    {
        if (!IsXRRunning)
            return;

        Debug.Log("Stopping XR...");

        XRGeneralSettings.Instance.Manager.StopSubsystems();
        XRGeneralSettings.Instance.Manager.DeinitializeLoader();

        IsXRRunning = false;

        Debug.Log("XR stopped completely.");
    }
}