using UnityEngine;
using Google.XR.Cardboard;

public class CardboardProfileApplier : MonoBehaviour
{
    [TextArea]
    [SerializeField] private string viewerProfileUri;

    public void ApplyProfile()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(viewerProfileUri))
        {
            Api.SaveDeviceParams(viewerProfileUri);
            Api.ReloadDeviceParams();
            Api.UpdateScreenParams();
        }
#endif
    }
}